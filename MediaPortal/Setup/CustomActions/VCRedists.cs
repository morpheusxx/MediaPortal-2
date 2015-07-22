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

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Deployment.WindowsInstaller;

namespace CustomActions
{
  public abstract class VCRedistInstaller : AbstractDownloadAndInstall
  {
    protected string _checkFile;
    protected Version _minVersion;

    protected VCRedistInstaller(Session session)
      : base(session)
    {
      _installerArgs = "/passive /norestart";
    }

    protected override bool IsInstallRequired()
    {
      string checkFile = Path.Combine(Environment.SystemDirectory, _checkFile);
      if (!File.Exists(checkFile))
        return true;

      FileVersionInfo fi = FileVersionInfo.GetVersionInfo(checkFile);
      Version fVersion = Version.Parse(fi.FileVersion);
      return fVersion < _minVersion;
    }
  }

  public class VC2008Installer : VCRedistInstaller
  {
    public VC2008Installer(Session session)
      : base(session)
    {
      _componentUrl = "http://download.microsoft.com/download/d/d/9/dd9a82d0-52ef-40db-8dab-795376989c03/vcredist_x86.exe";
      _installerArgs = "/q /norestart";
      _componentFileName = "vcredist_x86_2008SP1.exe";
      _componentName = "VCRedist 2008SP1 (x86)";
      _checkFile = "msvcr90.dll";
      _minVersion = new Version(9, 0, 30729, 6161);
    }

    /// <summary>
    /// This Custom Action will check if VC2008SP1 redistributables are currently installed by checking the registry and download + silently install it when not found.
    /// </summary>
    /// <param name="session"></param>
    /// <returns><see cref="ActionResult.Success"/>, if no exception happens and the install was executed, <see cref="ActionResult.NotExecuted"/> when component was already present and <see cref="ActionResult.Failure"/> on error.</returns>
    [CustomAction]
    public static ActionResult InstallVC2008SP1(Session session)
    {
      return new VC2008Installer(session).Install();
    }
  }

  public class VC2010Installer : VCRedistInstaller
  {
    public VC2010Installer(Session session)
      : base(session)
    {
      _componentUrl = "http://download.microsoft.com/download/5/B/C/5BC5DBB3-652D-4DCE-B14A-475AB85EEF6E/vcredist_x86.exe";
      _componentFileName = "vcredist_x86_2010.exe";
      _componentName = "VCRedist 2010 (x86)";
      _checkFile = "msvcr100.dll";
      _minVersion = new Version(10, 0, 40219, 325);
    }

    /// <summary>
    /// This Custom Action will check if VC2010 redistributables are currently installed by checking the registry and download + silently install it when not found.
    /// </summary>
    /// <param name="session"></param>
    /// <returns><see cref="ActionResult.Success"/>, if no exception happens and the install was executed, <see cref="ActionResult.NotExecuted"/> when component was already present and <see cref="ActionResult.Failure"/> on error.</returns>
    [CustomAction]
    public static ActionResult InstallVC2010(Session session)
    {
      return new VC2010Installer(session).Install();
    }
  }

  public class VC2013Installer : VCRedistInstaller
  {
    public VC2013Installer(Session session)
      : base(session)
    {
      _componentUrl = "http://download.microsoft.com/download/2/E/6/2E61CFA4-993B-4DD4-91DA-3737CD5CD6E3/vcredist_x86.exe";
      _componentFileName = "vcredist_x86_2013.exe";
      _componentName = "VCRedist 2013(x86)";
      _checkFile = "msvcr120.dll";
      _minVersion = new Version(12, 0, 21005, 1);
    }

    /// <summary>
    /// This Custom Action will check if VC2010 redistributables are currently installed by checking the registry and download + silently install it when not found.
    /// </summary>
    /// <param name="session"></param>
    /// <returns><see cref="ActionResult.Success"/>, if no exception happens and the install was executed, <see cref="ActionResult.NotExecuted"/> when component was already present and <see cref="ActionResult.Failure"/> on error.</returns>
    [CustomAction]
    public static ActionResult InstallVC2013(Session session)
    {
      return new VC2013Installer(session).Install();
    }
  }
}
