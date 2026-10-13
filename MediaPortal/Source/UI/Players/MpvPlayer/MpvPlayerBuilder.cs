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
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.UI.Presentation.Players;

namespace MediaPortal.UI.Players.Mpv
{
  /// <summary>
  /// Creates <see cref="MpvPlayer"/> instances for video media items.
  /// </summary>
  public class MpvPlayerBuilder : IPlayerBuilder
  {
    /// <summary>
    /// File extensions of video files whose media items don't provide a MIME type.
    /// </summary>
    protected static readonly HashSet<string> VIDEO_EXTENSIONS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        ".avi", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".dvr-ms", ".wtv", ".mp4", ".m4v", ".mkv", ".mk3d", ".webm", ".mov", ".flv",
        ".ogm", ".ogv", ".wmv", ".vob", ".ifo", ".3gp", ".divx", ".rm", ".rmvb", ".asf"
      };

    #region IPlayerBuilder implementation

    public IPlayer GetPlayer(MediaItem mediaItem)
    {
      string mimeType;
      string title;
      if (!mediaItem.GetPlayData(out mimeType, out title))
        return null;

      IResourceLocator locator = mediaItem.GetResourceLocator();
      if (!IsVideo(locator, mimeType))
        return null;

      MpvPlayer player = new MpvPlayer();
      try
      {
        player.SetMediaItem(locator, title, mediaItem);
      }
      catch (Exception e)
      { // The file might be broken, so the player wasn't able to play it
        ServiceRegistration.Get<ILogger>().Warn("MpvPlayer: Unable to play '{0}'", e, locator);
        player.Dispose();
        throw;
      }
      return player;
    }

    #endregion

    protected static bool IsVideo(IResourceLocator locator, string mimeType)
    {
      if (!string.IsNullOrEmpty(mimeType) && mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        return true;
      // 2nd chance: If no MIME type matches, try the extension
      string path = locator.NativeResourcePath.LastPathSegment.Path;
      int index = path.LastIndexOf('.');
      return index >= 0 && VIDEO_EXTENSIONS.Contains(path.Substring(index));
    }
  }
}
