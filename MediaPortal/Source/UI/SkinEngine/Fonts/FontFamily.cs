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
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Fonts
{
  /// <summary>
  /// A font family which was loaded from a font file (TrueType or OpenType).
  /// </summary>
  public class FontFamily : IDisposable
  {
    private readonly string _name;
    private readonly SKTypeface _typeface;

    public FontFamily(string name, string filePathName)
    {
      _name = name;
      // Load the requested font.
      _typeface = SKTypeface.FromFile(filePathName);
      if (_typeface == null)
        throw new ArgumentException("Failed to load font file '" + filePathName + "'.");
    }

    public string Name
    {
      get { return _name; }
    }

    public SKTypeface Typeface
    {
      get { return _typeface; }
    }

    public void Dispose()
    {
      if (_typeface != null)
        _typeface.Dispose();
    }
  }
}
