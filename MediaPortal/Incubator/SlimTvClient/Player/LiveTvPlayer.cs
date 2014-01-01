#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Common;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;
using MediaPortal.Plugins.SlimTv.Interfaces.LiveTvMediaItem;
using MediaPortal.UI.Players.Video;
using MediaPortal.UI.Players.Video.Interfaces;
using MediaPortal.UI.Players.Video.Tools;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SharpDX;
using SharpDX.Direct3D9;

namespace MediaPortal.Plugins.SlimTv.Client.Player
{
  public class LiveTvPlayer : TsVideoPlayer, IUIContributorPlayer, IReusablePlayer
  {
    #region Variables

    protected IList<ITimeshiftContext> _timeshiftContexes;
    protected StreamInfoHandler _chapterInfo = null;
    protected static TimeSpan TIMESPAN_LIVE = TimeSpan.FromMilliseconds(50);
    protected bool _zapping; // Indicates that we are currently changing a channel.

    #endregion

    #region ctor

    /// <summary>
    /// Constructs a LiveTvPlayer player object.
    /// </summary>
    public LiveTvPlayer()
    {
      PlayerTitle = "LiveTvPlayer"; // for logging
    }

    #endregion

    #region IUIContributorPlayer Member

    public Type UIContributorType
    {
      get { return typeof(SlimTvUIContributor); }
    }

    #endregion

    public ITimeshiftContext CurrentTimeshiftContext
    {
      get
      {
        return GetContext(CurrentTime);
      }
    }

    private TimeSpan GetStartDuration(int chapterIndex)
    {
      lock (SyncObj)
      {
        if (_timeshiftContexes == null || chapterIndex >= _timeshiftContexes.Count)
          return TimeSpan.Zero;
        return _timeshiftContexes[chapterIndex].TuneInTime - _timeshiftContexes[0].TuneInTime;
      }
    }

    private ITimeshiftContext GetContext(TimeSpan timeSpan)
    {
      IList<ITimeshiftContext> timeshiftContexes;
      lock (SyncObj)
        timeshiftContexes = _timeshiftContexes;

      if (timeshiftContexes == null)
        return null;

      TimeSpan totalTime = new TimeSpan();
      foreach (ITimeshiftContext timeshiftContext in timeshiftContexes)
      {
        if (timeSpan >= totalTime &&
          (
            (timeSpan <= totalTime + timeshiftContext.TimeshiftDuration) || timeshiftContext.TimeshiftDuration.TotalSeconds == 0 /* currently playing */
          ))
          return timeshiftContext;

        totalTime += timeshiftContext.TimeshiftDuration;
      }
      return null;
    }

    private void SeekChapter(bool next)
    {
      IList<ITimeshiftContext> timeshiftContexes;
      lock (SyncObj)
        timeshiftContexes = _timeshiftContexes;

      if (timeshiftContexes == null)
        return;

      TimeSpan timeSpan = CurrentTime;
      TimeSpan totalTime = new TimeSpan();
      int index = 0;
      bool found = false;
      foreach (ITimeshiftContext timeshiftContext in timeshiftContexes)
      {
        if (timeSpan >= totalTime &&
          (
          (timeSpan <= totalTime + timeshiftContext.TimeshiftDuration) ||
          timeshiftContext.TimeshiftDuration.TotalSeconds == 0 /* currently playing */
          ))
        {
          found = true;
          break;
        }
        index++;
        totalTime += timeshiftContext.TimeshiftDuration;
      }

      if (!found)
        return;

      if (next && index < timeshiftContexes.Count - 1)
        CurrentTime = GetStartDuration(index + 1);

      if (!next && index > 0)
        CurrentTime = GetStartDuration(index - 1);
    }

    protected override void EnumerateChapters(bool forceRefresh)
    {
      StreamInfoHandler chapterInfo;
      lock (SyncObj)
        chapterInfo = _chapterInfo;

      if (chapterInfo != null && !forceRefresh)
        return;

      IPlayerContextManager playerContextManager = ServiceRegistration.Get<IPlayerContextManager>();
      for (int index = 0; index < playerContextManager.NumActivePlayerContexts; index++)
      {
        IPlayerContext playerContext = playerContextManager.GetPlayerContext(index);
        if (playerContext == null || playerContext.CurrentPlayer != this)
          continue;

        LiveTvMediaItem liveTvMediaItem = playerContext.CurrentMediaItem as LiveTvMediaItem;
        if (liveTvMediaItem == null)
          continue;

        _timeshiftContexes = liveTvMediaItem.TimeshiftContexes;
        chapterInfo = new StreamInfoHandler();
        int i = 0;
        foreach (ITimeshiftContext timeshiftContext in _timeshiftContexes)
          chapterInfo.AddUnique(new StreamInfo(null, i++, GetContextTitle(timeshiftContext), 0));
      }
      lock (SyncObj)
        _chapterInfo = chapterInfo;
    }

    protected string GetContextTitle(ITimeshiftContext timeshiftContext)
    {
      if (timeshiftContext == null)
        return string.Empty;

      string program = timeshiftContext.Program != null ? timeshiftContext.Program.Title :
        ServiceRegistration.Get<ILocalization>().ToString("[SlimTvClient.NoProgram]");
      return string.Format("{0}: {1}", timeshiftContext.Channel.Name, program);
    }

    public void BeginZap()
    {
      ServiceRegistration.Get<ILogger>().Debug("{0}: Begin zapping", PlayerTitle);
      // Set indicator for zapping to blank the video surface with black.
      _zapping = true;
      // Tell the TsReader that we are zapping, before we actually tune the new channel.
      ((ITsReader)_sourceFilter).OnZapping(0x80);
    }

    public void EndZap()
    {
      SeekToEnd();
      Resume();

      // Clear any subtitle that might be currently displayed
      _subtitleRenderer.Reset();
      EnumerateStreams(true);
      EnumerateChapters(true);
      SetPreferredSubtitle();

      // First reset zapping indicator
      _zapping = false;
      // Then invalidate the "black" surface to use new frame.
      OnTextureInvalidated();
      ServiceRegistration.Get<ILogger>().Debug("{0}: End zapping", PlayerTitle);
    }

    public void OnProgramChange()
    {
      EnumerateChapters(true);
    }

    protected override void PostProcessTexture(Surface targetSurface)
    {
      if (_zapping)
      {
        // While zapping fill the current video frame with black. This avoids a frozen last frame from previous channel.
        SkinContext.Device.ColorFill(targetSurface, Color.Black);
      }
      else
        base.PostProcessTexture(targetSurface);
    }

    /// <summary>
    /// Checks the current stream position and seeks to end, if it is less than <see cref="TIMESPAN_LIVE"/> behind the live point.
    /// </summary>
    /// <returns><c>true</c> if seeked to end.</returns>
    protected bool SeekToEnd()
    {
      // Call a seek only if the stream is not "live"
      if (Duration - CurrentTime > TIMESPAN_LIVE)
      {
        ServiceRegistration.Get<ILogger>().Debug("{0}: SeekToEnd: Duration: {1}, CurrentTime: {2}", PlayerTitle, Duration, CurrentTime);
        CurrentTime = Duration; // Seek to end
        return true;
      }
      return false;
    }

    #region IChapterPlayer overrides

    public override string[] Chapters
    {
      get
      {
        EnumerateChapters();
        StreamInfoHandler chapters;
        lock (SyncObj)
          chapters = _chapterInfo;

        return chapters == null || chapters.Count == 0 ? EMPTY_STRING_ARRAY : chapters.GetStreamNames();
      }
    }

    public override void SetChapter(string chapter)
    {
      StreamInfoHandler chapters;
      lock (SyncObj)
        chapters = _chapterInfo;

      if (chapters == null || chapters.Count == 0)
        return;

      StreamInfo chapterInfo = chapters.FindStream(chapter);
      if (chapterInfo != null)
        CurrentTime = GetStartDuration(chapterInfo.StreamIndex);
    }

    public override void NextChapter()
    {
      SeekChapter(true);
    }

    public override void PrevChapter()
    {
      SeekChapter(false);
    }

    public override string CurrentChapter
    {
      get
      {
        return GetContextTitle(GetContext(CurrentTime));
      }
    }

    #endregion

    #region IReusablePlayer members

    public event RequestNextItemDlgt NextItemRequest;

    public bool NextItem(MediaItem mediaItem, StartTime startTime)
    {
      string mimeType;
      string title;
      if (!mediaItem.GetPlayData(out mimeType, out title) || mimeType != LiveTvMediaItem.MIME_TYPE_TV)
      {
        ServiceRegistration.Get<ILogger>().Debug("SlimTvHandler: Cannot reuse current player for new mimetype {0}", mimeType);
        return false;
      }
      Stop();
      // Set new resource locator for existing player, this avoids interim close of player slot
      IResourceLocator resourceLocator = mediaItem.GetResourceLocator();
      ServiceRegistration.Get<ILogger>().Debug("SlimTvHandler: Changing file/stream for player to {0}", resourceLocator.NativeResourcePath);
      SetMediaItem(resourceLocator, mimeType);
      return true;
    }

    #endregion
  }
}