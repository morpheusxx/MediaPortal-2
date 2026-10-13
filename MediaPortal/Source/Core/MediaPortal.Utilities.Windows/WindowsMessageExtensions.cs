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

using System.Windows.Forms;
using MediaPortal.UI.General;

namespace MediaPortal.Utilities
{
  /// <summary>
  /// Conversion between the portable <see cref="WindowsMessage"/>, which is broadcasted by the main window, and the WinForms
  /// <see cref="Message"/> which is used by Windows specific input plugins.
  /// </summary>
  public static class WindowsMessageExtensions
  {
    public static Message ToMessage(this WindowsMessage message)
    {
      Message result = Message.Create(message.HWnd, message.Msg, message.WParam, message.LParam);
      result.Result = message.Result;
      return result;
    }

    public static WindowsMessage ToWindowsMessage(this Message message)
    {
      return new WindowsMessage { HWnd = message.HWnd, Msg = message.Msg, WParam = message.WParam, LParam = message.LParam, Result = message.Result };
    }
  }
}
