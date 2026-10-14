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
using System.Runtime.InteropServices;

namespace MediaPortal.UI.SkinEngine.Utils
{
  /// <summary>
  /// Platform independent access to system settings which are used by the controls.
  /// </summary>
  public static class SystemSettings
  {
    private const uint SPI_GETWHEELSCROLLLINES = 0x0068;
    private const int DEFAULT_WHEEL_SCROLL_LINES = 3;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

    /// <summary>
    /// Gets the number of lines to scroll for one mouse wheel detent.
    /// </summary>
    public static int MouseWheelScrollLines
    {
      get
      {
        if (OperatingSystem.IsWindows())
        {
          try
          {
            uint lines = 0;
            if (SystemParametersInfo(SPI_GETWHEELSCROLLLINES, 0, ref lines, 0) && lines > 0 && lines < 100)
              return (int) lines;
          }
          catch (Exception)
          { }
        }
        return DEFAULT_WHEEL_SCROLL_LINES;
      }
    }
  }
}
