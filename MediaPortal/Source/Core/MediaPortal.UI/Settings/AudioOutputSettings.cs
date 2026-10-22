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
using MediaPortal.Common.Settings;

namespace MediaPortal.UI.Settings
{
  /// <summary>
  /// Platform independent setting for the audio output device of the players.
  /// </summary>
  /// <remarks>
  /// The device is selected by its (display) name, which each player matches against the names of the devices of its audio backend
  /// (see <see cref="AudioDeviceMatcher"/>). An empty value selects the default device of the system. On Windows the BASS player uses its own
  /// settings (DirectSound/WASAPI device) in addition.
  /// </remarks>
  public class AudioOutputSettings
  {
    /// <summary>
    /// Gets or sets the name of the audio output device. Empty for the default device of the system.
    /// </summary>
    [Setting(SettingScope.Global, "")]
    public string AudioDevice { get; set; }
  }

  /// <summary>
  /// Finds the audio device which was selected by name in the list of the names a player's audio backend provides.
  /// </summary>
  public static class AudioDeviceMatcher
  {
    /// <summary>
    /// Returns the index of the device whose name is identical (ignoring case) to <paramref name="wanted"/> or, if there is no such device,
    /// the only one which contains it. Returns <c>-1</c> if there is no match or the match is ambiguous.
    /// </summary>
    public static int Find(string wanted, IList<string> deviceNames)
    {
      if (string.IsNullOrWhiteSpace(wanted))
        return -1;
      wanted = wanted.Trim();
      for (int i = 0; i < deviceNames.Count; i++)
        if (string.Equals(deviceNames[i], wanted, StringComparison.OrdinalIgnoreCase))
          return i;
      int found = -1;
      for (int i = 0; i < deviceNames.Count; i++)
        if (deviceNames[i] != null && deviceNames[i].IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
        {
          if (found != -1)
            return -1;
          found = i;
        }
      return found;
    }
  }
}
