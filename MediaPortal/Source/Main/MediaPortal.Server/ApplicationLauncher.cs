#region Copyright (C) 2007-2020 Team MediaPortal

/*
    Copyright (C) 2007-2020 Team MediaPortal
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
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandLine;
using MediaPortal.Common;
using MediaPortal.Common.Exceptions;
using MediaPortal.Common.Logging;
using MediaPortal.Common.UI;
using MediaPortal.Common.Runtime;

[assembly: CLSCompliant(true)]
namespace MediaPortal.Server
{
  public class ApplicationLauncher
  {
    protected static WindowsService _windowsService;
    protected ServerRuntime _runtime;

    public ApplicationLauncher(string dataDirectory)
    {
      _runtime = new ServerRuntime(dataDirectory, _windowsService == null ? "ServerConsole" : "ServerService", OnShutdownRequested);
    }

    /// <summary>
    /// The main entry point for the MP2 server application.
    /// </summary>
    public static void Main(params string[] args)
    {
      // Parse command line options
      CommandLineOptions mpOptions = new CommandLineOptions();
      Parser.Default.ParseArguments<CommandLineOptions>(args)
        .WithParsed(parsed => mpOptions = parsed)
        .WithNotParsed(err => Environment.Exit(1));

      if (mpOptions.RunAsConsoleApp)
      {
        new ApplicationLauncher(mpOptions.DataDirectory).RunAsConsole();
      }
      else
      {
        _windowsService = new WindowsService();
        ServiceBase[] servicesToRun = new ServiceBase[] { _windowsService };
        ServiceBase.Run(servicesToRun);
      }
    }

    /// <summary>
    /// Gets the suspend level requested by the server (see <see cref="ServerRuntime.SuspendLevel"/>).
    /// </summary>
    public SuspendLevel SuspendLevel
    {
      get { return _runtime.SuspendLevel; }
    }

    public void Start()
    {
      _runtime.Start();
    }

    public void Stop()
    {
      _runtime.Stop();
    }

    /// <summary>
    /// Called when a shutdown was requested via IPC.
    /// </summary>
    private bool OnShutdownRequested()
    {
      if (_windowsService == null)
      {
        Application.Exit();
        return true;
      }

      // invoke service shutdown asynchronous with a little delay
      ThreadPool.QueueUserWorkItem(_ =>
      {
        Thread.Sleep(500);
        try
        {
          _windowsService.Stop();
        }
        catch
        {
          // ignored
        }
      });
      return true;
    }

    protected void RunAsConsole()
    {
      Application.ThreadException += LauncherExceptionHandling.Application_ThreadException;
      AppDomain.CurrentDomain.UnhandledException += LauncherExceptionHandling.CurrentDomain_UnhandledException;
      TaskScheduler.UnobservedTaskException += LauncherExceptionHandling.TaskScheduler_UnobservedTaskException;

      Start();

      try
      {
        Application.EnableVisualStyles();
        FormDpiAwarenessExtension.TryEnableDPIAwareness();
        Application.Run(new MainForm());
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Critical("Error executing application", e);
      }

      Stop();
      Application.ThreadException -= LauncherExceptionHandling.Application_ThreadException;
      AppDomain.CurrentDomain.UnhandledException -= LauncherExceptionHandling.CurrentDomain_UnhandledException;
      TaskScheduler.UnobservedTaskException -= LauncherExceptionHandling.TaskScheduler_UnobservedTaskException;
    }
  }
}
