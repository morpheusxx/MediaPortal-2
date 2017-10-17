#region Copyright (C) 2007-2017 Team MediaPortal

/*
    Copyright (C) 2007-2017 Team MediaPortal
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

using System.Collections.Generic;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Extensions.MetadataExtractors.Aspects;
using MediaPortal.UiComponents.Media.MediaItemActions;

namespace MediaPortal.Plugins.SlimTv.Client.MediaItemActions
{
  public class DeleteRecordingFromStorage : DeleteFromStorage
  {
    protected bool _isCurrentlyRecording;

    public DeleteRecordingFromStorage()
    {
      _defaultRules.Add(new DeleteRule
      {
        IsEnabled = true,
        DeleteEmptyFolders = true,
        HasAspectGuid = RecordingAspect.ASPECT_ID,
        DeleteOtherExtensions = new List<string> { ".xml", ".jpg" } /* Standard .xml file of recording and optional created thumbnail */
      });
    }

    public override bool IsAvailable(MediaItem mediaItem)
    {
      return IsRecording(mediaItem) && IsResourceDeletor(mediaItem);
    }

    public override bool Process(MediaItem mediaItem, out ContentDirectoryMessaging.MediaItemChangeType changeType)
    {
      // TODO: Check if media item is a currently running recording
      _isCurrentlyRecording = false;
      var result = base.Process(mediaItem, out changeType);
      return result;
    }

    public override string ConfirmationMessage
    {
      get
      {
        return _isCurrentlyRecording ?
          "[SlimTvClient.DeleteRecording.Confirmation]" :
          "[Media.DeleteFromStorage.Confirmation]";
      }
    }
  }
}
