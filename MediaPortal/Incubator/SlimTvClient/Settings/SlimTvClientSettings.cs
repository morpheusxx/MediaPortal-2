﻿#region Copyright (C) 2007-2013 Team MediaPortal

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

using MediaPortal.Common.Settings;

namespace MediaPortal.Plugins.SlimTv.Client.Settings
{
  class SlimTvClientSettings
  {
    /// <summary>
    /// Defines the number of rows to be visible in EPG.
    /// </summary>
    [Setting(SettingScope.User, DefaultValue = 7)]
    public int EpgNumberOfRows { get; set; }

    /// <summary>
    /// Defines the number of hours to be visible in EPG.
    /// </summary>
    [Setting(SettingScope.User, DefaultValue = 2d)]
    public double EpgVisibleHours { get; set; }
  }
}
