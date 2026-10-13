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

using MediaPortal.UI.Control.InputManager;
using SilkKey = Silk.NET.Input.Key;

namespace MediaPortal.UI.SkinEngine.InputManagement
{
  /// <summary>
  /// Maps keyboard events to <see cref="Key"/> instances.
  /// </summary>
  public static class InputMapper
  {
    /// <summary>
    /// Maps the key of a key down event to a MediaPortal key.
    /// </summary>
    /// <param name="keycode">The pressed key.</param>
    /// <param name="alt">Alt is pressed.</param>
    /// <param name="shift">Shift is pressed.</param>
    /// <param name="control">Control is pressed.</param>
    /// <param name="superPressed">The Windows/Super key is pressed. Enter is not mapped then, the operating system uses this combination.</param>
    /// <returns>The mapped key or <see cref="Key.None"/>, if the key isn't a special key. Printable characters are mapped by
    /// <see cref="MapPrintableKeys"/>.</returns>
    public static Key MapSpecialKey(SilkKey keycode, bool alt, bool shift, bool control, bool superPressed)
    {
      if (alt && !(shift || control))
      {
        switch (keycode)
        {
          case SilkKey.KeypadAdd:
            return Key.VolumeUp;
          case SilkKey.KeypadSubtract:
            return Key.VolumeDown;
          case SilkKey.Enter:
          case SilkKey.KeypadEnter:
            if (!superPressed)
              return Key.Fullscreen;
            break;
          case SilkKey.F4:
            return Key.Close;
        }
      }
      else if (control && !(shift || alt))
      {
        switch (keycode)
        {
          case SilkKey.X:
            return Key.Cut;
          case SilkKey.C:
            return Key.Copy;
          case SilkKey.V:
            return Key.Paste;
        }
      }
      else if (shift && !(alt || control))
      {
        switch (keycode)
        {
          case SilkKey.F10:
            return Key.ContextMenu;
        }
      }
      else if (!shift && !alt && !control)
      {
        switch (keycode)
        {
          case SilkKey.Delete:
            return Key.Delete;
          case SilkKey.Insert:
            return Key.Insert;
          case SilkKey.Enter:
          case SilkKey.KeypadEnter:
            if (!superPressed)
              return Key.Ok;
            break;
          case SilkKey.Backspace:
            return Key.BackSpace;
          case SilkKey.Escape:
            return Key.Escape;

          case SilkKey.Pause:
            return Key.Pause;

          case SilkKey.Up:
            return Key.Up;
          case SilkKey.Down:
            return Key.Down;
          case SilkKey.Left:
            return Key.Left;
          case SilkKey.Right:
            return Key.Right;

          case SilkKey.PageUp:
            return Key.PageUp;
          case SilkKey.PageDown:
            return Key.PageDown;

          case SilkKey.Home:
            return Key.Home;
          case SilkKey.End:
            return Key.End;

          case SilkKey.Menu:
            return Key.ContextMenu;

          case SilkKey.F1:
            return Key.F1;
          case SilkKey.F2:
            return Key.F2;
          case SilkKey.F3:
            return Key.F3;
          case SilkKey.F4:
            return Key.F4;
          case SilkKey.F5:
            return Key.F5;
          case SilkKey.F6:
            return Key.F6;
          case SilkKey.F7:
            return Key.F7;
          case SilkKey.F8:
            return Key.F8;
          case SilkKey.F9:
            return Key.F9;
          case SilkKey.F10:
            return Key.F10;
          case SilkKey.F11:
            return Key.F11;
          case SilkKey.F12:
            return Key.F12;
          case SilkKey.F13:
            return Key.F13;
          case SilkKey.F14:
            return Key.F14;
          case SilkKey.F15:
            return Key.F15;
          case SilkKey.F16:
            return Key.F16;
          case SilkKey.F17:
            return Key.F17;
          case SilkKey.F18:
            return Key.F18;
          case SilkKey.F19:
            return Key.F19;
          case SilkKey.F20:
            return Key.F20;
          case SilkKey.F21:
            return Key.F21;
          case SilkKey.F22:
            return Key.F22;
          case SilkKey.F23:
            return Key.F23;
          case SilkKey.F24:
            return Key.F24;
        }
      }
      return Key.None;
    }

    /// <summary>
    /// Maps the character of a key typed event to a MediaPortal key.
    /// </summary>
    public static Key MapPrintableKeys(char keyChar)
    {
      if (keyChar >= (char) 32)
        return new Key(keyChar);
      return Key.None;
    }
  }
}
