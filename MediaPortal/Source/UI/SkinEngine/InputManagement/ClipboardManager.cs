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
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Control.InputManager;

namespace MediaPortal.UI.SkinEngine.InputManagement
{
  /// <summary>
  /// Provides access to the text contents of the system clipboard. The access to the clipboard is implemented by the
  /// window host, which registers its accessors in <see cref="GetTextAccessor"/> and <see cref="SetTextAccessor"/>.
  /// </summary>
  public class ClipboardManager : IClipboardManager
  {
    #region Static fields and instance

    private static readonly object _syncObj = new object();
    private static ClipboardManager _instance;

    public static ClipboardManager Instance
    {
      get
      {
        lock (_syncObj)
          if (_instance == null)
            _instance = new ClipboardManager();
        return _instance;
      }
    }

    #endregion

    /// <summary>
    /// Function which returns the text of the clipboard. Must be set by the window host; it is called in the thread of the caller.
    /// </summary>
    internal Func<string> GetTextAccessor { get; set; }

    /// <summary>
    /// Action which sets the text of the clipboard. Must be set by the window host; it is called in the thread of the caller.
    /// </summary>
    internal Action<string> SetTextAccessor { get; set; }

    /// <summary>
    /// Tries to get text contents from the clipboard. If the clipboard contains any other type then text, the returned result will be <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> if text could be retrieved.</returns>
    public bool GetClipboardText(out string text)
    {
      text = null;
      Func<string> accessor = GetTextAccessor;
      if (accessor == null)
        return false;
      try
      {
        text = accessor();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("ClipboardManager: Error getting text data from clipboard.", ex);
      }
      return !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// Tries to set text contents into the clipboard.
    /// </summary>
    /// <param name="text">Text to copy.</param>
    /// <returns><c>true</c> if text was set.</returns>
    public bool SetClipboardText(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return false;
      Action<string> accessor = SetTextAccessor;
      if (accessor == null)
        return false;
      try
      {
        accessor(text);
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("ClipboardManager: Error setting text data into clipboard.", ex);
        return false;
      }
    }
  }
}
