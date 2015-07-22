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
using System.Net;
using Microsoft.Deployment.WindowsInstaller;

namespace CustomActions
{
  public abstract class AbstractDownloadAndInstall
  {
    protected Session _session;
    protected string _componentName;
    protected string _componentFileName;
    protected string _componentUrl;
    protected string _downloadedFile;
    protected string _installerArgs;

    #region CompressionWebClient

    /// <summary>
    /// Small helper class that enables compression and a shorter request timeout.
    /// </summary>
    class CompressionWebClient : WebClient
    {
      protected override WebRequest GetWebRequest(Uri address)
      {
        Headers["Accept-Encoding"] = "gzip,deflate";
        HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
        request.Timeout = 15000; // use 15 seconds - default is 100 seconds
        request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
        return request;
      }
    }

    #endregion

    #region Constructor

    protected AbstractDownloadAndInstall(Session session)
    {
      _session = session;
    }

    #endregion

    #region Public members

    public ActionResult Install()
    {
      try
      {
        if (!IsInstallRequired())
          return ActionResult.NotExecuted;

        if (!Download())
          return ActionResult.Failure;

        return RunInstaller() ? ActionResult.Success : ActionResult.Failure;
      }
      catch (Exception ex)
      {
        Log("Error: " + ex.Message);
        return ActionResult.Failure;
      }
    }

    #endregion

    #region Protected and abstract members

    /// <summary>
    /// Performs checks to decide if a component needs to be installed.
    /// </summary>
    /// <returns><c>true</c> if installation is required</returns>
    protected abstract bool IsInstallRequired();

    protected void Log(string format, params object[] args)
    {
      var msg = string.Format(format, args);
      _session.Log("{0}: {1}", _componentName, msg);
    }

    protected virtual bool Download()
    {
      _downloadedFile = Path.Combine(Path.GetTempPath(), _componentFileName);
      // if temp file is already present it was probably downloaded earlier - use it
      if (!File.Exists(_downloadedFile))
      {
        var client = new CompressionWebClient();
        Log("Downloading installer...");
        client.DownloadFile(_componentUrl, _downloadedFile);
        client.Dispose();
      }
      else
      {
        Log("Using existing downloaded file");
      }
      return File.Exists(_downloadedFile);
    }

    protected virtual bool RunInstaller()
    {
      // 3. run installer
      Log("Running installer from '{0}'", _downloadedFile);
      using (var process = Process.Start(_downloadedFile, _installerArgs))
      {
        // wait max. 1 minute for the installer to finish
        if (process.WaitForExit(1000 * 60))
        {
          process.Close();
          Log("Successfully installed");
          return true;
        }
      }
      Log("Installation failed");
      return false;
    }

    #endregion
  }
}
