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
using System.Linq;
using MediaPortal.UI.Settings;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Configuration.ConfigurationClasses;
using MediaPortal.UI.SkinEngine.GUI;
using Silk.NET.Windowing;

namespace MediaPortal.UI.SkinEngine.Settings.Configuration.Appearance
{
  public class StartupScreen : SingleSelectionList
  {
    #region Base overrides

    public override void Load()
    {
      IList<IMonitor> monitors = MainWindow.GetMonitors();

      IList<String> options = new List<String>();
      options.Add("[Settings.Appearance.System.StartupScreen.DoNotUseSelection]");

      foreach (IMonitor monitor in monitors)
        options.Add(string.Format("{0} ({1}x{2})", string.IsNullOrEmpty(monitor.Name) ? "Monitor " + (monitor.Index + 1) : monitor.Name,
            monitor.Bounds.Size.X, monitor.Bounds.Size.Y));

      int startupScreenNum = SettingsManager.Load<StartupSettings>().StartupScreenNum;

      if (startupScreenNum < monitors.Count)
        Selected = startupScreenNum + 1;
      else
        Selected = 0;

      _items = options.Select(LocalizationHelper.CreateResourceString).ToList();
    }

    public override void Save()
    {
      StartupSettings settings = SettingsManager.Load<StartupSettings>();
      settings.StartupScreenNum = Selected - 1;
      SettingsManager.Save(settings);
    }

    #endregion
  }
}
