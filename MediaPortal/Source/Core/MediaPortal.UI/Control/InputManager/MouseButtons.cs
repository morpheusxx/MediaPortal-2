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

namespace MediaPortal.UI.Control.InputManager
{
  /// <summary>
  /// Mouse buttons. Values are identical to System.Windows.Forms.MouseButtons so raw values can be converted by a cast.
  /// </summary>
  [Flags]
  public enum MouseButtons
  {
    None = 0,
    Left = 0x100000,
    Right = 0x200000,
    Middle = 0x400000,
    XButton1 = 0x800000,
    XButton2 = 0x1000000
  }
}
