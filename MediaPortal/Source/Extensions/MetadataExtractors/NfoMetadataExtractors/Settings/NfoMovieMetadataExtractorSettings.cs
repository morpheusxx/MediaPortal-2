#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
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
using MediaPortal.Common.Settings;

namespace MediaPortal.Extensions.MetadataExtractors.NfoMetadataExtractors.Settings
{
  /// <summary>
  /// Settings class for the <see cref="NfoMovieMetadataExtractor"/>
  /// </summary>
  /// <remarks>
  /// Also contains all properties defined in <see cref="NfoMetadataExtractorSettingsBase"/>
  /// </remarks>
  public class NfoMovieMetadataExtractorSettings : NfoMetadataExtractorSettingsBase
  {
    #region Ctor

    /// <summary>
    /// Sets the default values specific to the <see cref="NfoMovieMetadataExtractor"/>
    /// </summary>
    public NfoMovieMetadataExtractorSettings()
    {
      MovieNfoFileNames = new HashSet<string> { "movie" };
    }

    #endregion

    #region Public properties

    /// <summary>
    /// These file names are used additionally to the media file name to find a respective nfo-file for movies
    /// </summary>
    [Setting(SettingScope.Global)]
    public HashSet<string> MovieNfoFileNames { get; set; }

    #endregion
  }
}
