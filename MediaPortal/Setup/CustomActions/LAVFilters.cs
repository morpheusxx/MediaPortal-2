#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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

using System.IO;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace CustomActions
{
  public class LAVInstaller : AbstractDownloadAndInstall
  {
    const string LAV_DOWNLOAD_URL = "http://install.team-mediaportal.com/LAVFilters.exe";
    const string LAV_AUDIO_REGISTRY_PATH = @"CLSID\{E8E73B6B-4CB3-44A4-BE99-4F7BCB96E491}\InprocServer32";
    const string LAV_VIDEO_REGISTRY_PATH = @"CLSID\{EE30215D-164F-4A92-A4EB-9D4C13390F9F}\InprocServer32";

    public LAVInstaller(Session session)
      : base(session)
    {
      _componentUrl = LAV_DOWNLOAD_URL;
      _componentName = "LAVFilters";
      _componentFileName = "LAVFilters.exe";
      _installerArgs = "/SILENT /SP-";
    }

    protected override bool IsInstallRequired()
    {
      Log("Checking if LAVFilters are registered");

      // 1. check if LAV Filters are registered
      RegistryKey audioKey = Registry.ClassesRoot.OpenSubKey(LAV_AUDIO_REGISTRY_PATH, false);
      RegistryKey videoKey = Registry.ClassesRoot.OpenSubKey(LAV_VIDEO_REGISTRY_PATH, false);
      if (audioKey != null && videoKey != null)
      {
        var audioFilterPath = audioKey.GetValue(null) as string;
        var videoFilterPath = videoKey.GetValue(null) as string;
        if (!string.IsNullOrEmpty(audioFilterPath) && !string.IsNullOrEmpty(videoFilterPath))
        {
          if (File.Exists(audioFilterPath) && File.Exists(videoFilterPath))
          {
            Log("Found Audio Filter at '{0}'", audioFilterPath);
            Log("Found Video Filter at '{0}'", videoFilterPath);
            return false;
          }
        }
      }
      return true;
    }

    /// <summary>
    /// This Custom Action will check if LAV filters are currently installed by checking the registry and download + silently install it when not found.
    /// </summary>
    /// <param name="session"></param>
    /// <returns><see cref="ActionResult.Success"/>, if no exception happens and the install was executed, <see cref="ActionResult.NotExecuted"/> when LAV Filters are already present and <see cref="ActionResult.Failure"/> on error.</returns>
    [CustomAction]
    public static ActionResult InstallLAVFilters(Session session)
    {
      return new LAVInstaller(session).Install();
    }
  }
}
