#region Copyright (C) 2007-2021 Team MediaPortal

/*
    Copyright (C) 2007-2021 Team MediaPortal
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
using MediaPortal.Common.Logging;
using ManagedBass.Cd;

namespace MediaPortal.Extensions.BassLibraries
{
  public class BassUtils
  {

    #region Fields

    protected static BassLibraryManager _lib;

    #endregion


    public class AudioTrack
    {
      protected byte _trackNo;
      protected double _duration;
      protected int _startHour;
      protected int _startMin;
      protected int _startSec;
      protected int _startFrame;

      public AudioTrack(byte trackNo, double duration, int startHour, int startMin, int startSec, int startFrame)
      {
        _trackNo = trackNo;
        _duration = duration;
        _startHour = startHour;
        _startMin = startMin;
        _startSec = startSec;
        _startFrame = startFrame;
      }

      /// <summary>
      /// Number of audio track, starting with <c>1</c>.
      /// </summary>
      public byte TrackNo
      {
        get { return _trackNo; }
      }

      /// <summary>
      /// Duration of the audio track in seconds.
      /// </summary>
      public double Duration
      {
        get { return _duration; }
      }

      /// <summary>
      /// Start hour of this audio track on the CD.
      /// </summary>
      public int StartHour
      {
        get { return _startHour; }
      }

      /// <summary>
      /// Start minute of this audio track on the CD.
      /// </summary>
      public int StartMin
      {
        get { return _startMin; }
      }

      /// <summary>
      /// Start second of this audio track on the CD.
      /// </summary>
      public int StartSec
      {
        get { return _startSec; }
      }

      /// <summary>
      /// Start frame of this audio track on the CD.
      /// </summary>
      public int StartFrame
      {
        get { return _startFrame; }
      }
    }

    protected static readonly object _syncObj = new object();

    /// <summary>
    /// Makes sure that the BASS library is loaded and initialized.
    /// </summary>
    public static void EnsureInitialized()
    {
      lock (_syncObj)
        if (_lib == null)
          _lib = BassLibraryManager.Get(); // Makes sure that the native libraries are found
    }

    /// <summary>
    /// Checks, if the media in the given drive Letter is a Red Book (Audio) CD.
    /// </summary>
    /// <param name="drive">Drive path or drive letter (<c>"F:"</c> or <c>"F"</c>).</param>
    /// <returns><c>true</c>, if the media in the given <paramref name="drive"/> is a Red Book CD.</returns>
    public static bool IsARedBookCD(string drive)
    {
      IList<AudioTrack> tracks = GetAudioTracks(drive);
      return tracks != null && tracks.Count > 0;
    }

    /// <summary>
    /// Returns the number of audio tracks of the CD in the given <paramref name="drive"/>.
    /// </summary>
    /// <param name="drive">Drive letter (<c>d:</c>) or drive root path (<c>d:\</c>).</param>
    /// <returns>Number of audio tracks of the current CD or <c>-1</c>, if an error occurs (for example: No CD present in
    /// the given <paramref name="drive"/>).</returns>
    public static int GetNumAudioTracks(string drive)
    {
      if (!OperatingSystem.IsWindows())
        return -1; // CD drives are addressed by their drive letter, TODO: other platforms
      lock (_syncObj)
        try
        {
          EnsureInitialized();
          if (string.IsNullOrEmpty(drive))
            return -1;
          char driveLetter = System.IO.Path.GetFullPath(drive).ToCharArray()[0];
          int driveId = Drive2BassID(driveLetter);

          return BassCd.GetTracks(driveId);
        }
        catch (Exception e)
        {
          ServiceRegistration.Get<ILogger>().Error("BassUtils: Error examining CD in drive '{0}'", e, drive);
          return -1;
        }
    }

    /// <summary>
    /// Returns the audio tracks of the CD in the given <paramref name="drive"/>.
    /// </summary>
    /// <param name="drive">Drive letter (<c>d:</c>) or drive root path (<c>d:\</c>).</param>
    /// <returns>Audio tracks of the current CD or <c>null</c>, if an error occurs (for example: No CD present in
    /// the given <paramref name="drive"/>).</returns>
    public static IList<AudioTrack> GetAudioTracks(string drive)
    {
      if (!OperatingSystem.IsWindows())
        return null; // CD drives are addressed by their drive letter, TODO: other platforms
      lock (_syncObj)
        try
        {
          EnsureInitialized();
          if (string.IsNullOrEmpty(drive))
            return null;
          char driveLetter = System.IO.Path.GetFullPath(drive).ToCharArray()[0];
          int driveId = Drive2BassID(driveLetter);

          TOC toc;
          if (!BassCd.GetTOC(driveId, TOCMode.Time, out toc))
            return null;
          IList<AudioTrack> result = new List<AudioTrack>(toc.Tracks.Count);
          int trackNo = 0; // BASS starts to count at track 0
          // Albert, 2011-07-30: Due to the spare documentation of the BASS library, I don't know the correct way...
          // It seems that this algorithm returns the correct number of audio tracks.
          foreach (TOCTrack track in toc.Tracks)
          {
            if ((track.Control & TOCControlFlags.DataTrack) == 0 && track.Track != 170) // 170 = lead-out (see BASS documentation)
            {
              // Length of the track in bytes (CD audio: 44100 Hz, 16 bit, stereo)
              double duration = BassCd.GetTrackLength(driveId, trackNo++) / (44100.0 * 2 * 2);
              TimeSpan address = track.Address; // Time mode: hours are returned as part of the minutes (see BASS documentation), the frames are given separately
              result.Add(new AudioTrack(track.Track, duration, (int) address.TotalHours, (int) address.TotalMinutes, address.Seconds, track.Frame));
            }
          }
          return result;
        }
        catch (Exception e)
        {
          ServiceRegistration.Get<ILogger>().Error("BassUtils: Error examining CD in drive '{0}'", e, drive);
          return null;
        }
    }

    /// <summary>
    /// Converts the given CD/DVD/BD drive letter to a number suiteable for BASS.
    /// </summary>
    /// <param name="driveLetter">Drive letter to convert.</param>
    /// <returns>Bass id of the given <paramref name="driveLetter"/>.</returns>
    public static int Drive2BassID(char driveLetter)
    {
      if (!OperatingSystem.IsWindows())
        return -1;
      lock (_syncObj)
      {
        EnsureInitialized();
        for (int i = 0; i < 26; i++)
        {
          CDInfo cdInfo;
          if (BassCd.GetInfo(i, out cdInfo) && cdInfo.DriveLetter == driveLetter)
            return i;
        }
      }
      return -1;
    }
  }
}
