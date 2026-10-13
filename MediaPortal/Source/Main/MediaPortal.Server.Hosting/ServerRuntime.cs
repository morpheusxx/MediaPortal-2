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
using System.IO;
using System.Linq;
using System.Threading;
using MediaPortal.Backend;
using MediaPortal.Backend.ClientCommunication;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.PathManager;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Runtime;
using MediaPortal.Common.Services.Logging;
using MediaPortal.Common.Services.PathManager;
using MediaPortal.Common.Services.Runtime;
using MediaPortal.Common.SystemResolver;
using MediaPortal.Utilities.Process;

namespace MediaPortal.Server
{
  /// <summary>
  /// Platform independent life cycle of the MP2 server: registers the services, starts the plugins and backend services and shuts
  /// everything down again. It contains no UI and no operating system specific hosting code; the hosts (Windows service / Windows
  /// console window / generic host for Linux and containers) call <see cref="Start"/> and <see cref="Stop"/>.
  /// </summary>
  public class ServerRuntime
  {
    protected readonly string _dataDirectory;
    protected readonly string _ipcName;
    protected readonly Func<bool> _shutdownRequested;

    protected SystemStateService _systemStateService;
    protected IpcServer _ipcServer;
    protected AsynchronousMessageQueue _messageQueue;
    protected volatile SuspendLevel _suspendLevel = SuspendLevel.None;

    /// <summary>
    /// Creates a new server runtime.
    /// </summary>
    /// <param name="dataDirectory">Optional directory to use instead of the default application data directory.</param>
    /// <param name="ipcName">Name for the IPC pipe (<c>null</c> to disable IPC). It is used by the service monitor and tools to request a shutdown.</param>
    /// <param name="shutdownRequested">Called when a shutdown was requested via IPC. The host has to stop the server asynchronously.
    /// Return <c>true</c> if the request was accepted.</param>
    public ServerRuntime(string dataDirectory, string ipcName, Func<bool> shutdownRequested)
    {
      _dataDirectory = dataDirectory;
      _ipcName = ipcName;
      _shutdownRequested = shutdownRequested;
    }

    /// <summary>
    /// Gets the suspend level currently requested by the server, e.g. to avoid standby while remote clients are connected.
    /// </summary>
    public SuspendLevel SuspendLevel
    {
      get { return _suspendLevel; }
    }

    /// <summary>
    /// Registers and starts all services of the server. Throws if the server could not be started.
    /// </summary>
    public void Start()
    {
      Thread.CurrentThread.Name = "Main";

#if !DEBUG
      string logPath = GetDefaultLogPath();
#endif

      _systemStateService = new SystemStateService();
      ServiceRegistration.Set<ISystemStateService>(_systemStateService);
      _systemStateService.SwitchSystemState(SystemState.Initializing, false);

      try
      {
        ILogger logger = null;
        try
        {
          // Check if user wants to override the default Application Data location.
          ApplicationCore.RegisterVitalCoreServices(true, _dataDirectory);
          ApplicationCore.RegisterCoreServices();
          logger = ServiceRegistration.Get<ILogger>();

#if !DEBUG
          IPathManager pathManager = ServiceRegistration.Get<IPathManager>();
          logPath = pathManager.GetPath("<LOG>");
#endif

          BackendExtension.RegisterBackendServices();
        }
        catch (Exception e)
        {
          if (logger != null)
            logger.Critical("Error starting application", e);
          _systemStateService.SwitchSystemState(SystemState.ShuttingDown, true);
          ServiceRegistration.IsShuttingDown = true;

          BackendExtension.DisposeBackendServices();
          ApplicationCore.DisposeCoreServices();

          throw;
        }

        // Start the core
        logger.Debug("ServerRuntime: Starting core");

        try
        {
          var mediaAccessor = ServiceRegistration.Get<IMediaAccessor>();
          var pluginManager = ServiceRegistration.Get<IPluginManager>();
          pluginManager.Initialize();
          pluginManager.Startup(false);
          ApplicationCore.StartCoreServices();
          InitIpc();
          BackendExtension.StartupBackendServices();

          mediaAccessor.Initialize();

          logger.Info("Switching to running state");
          _systemStateService.SwitchSystemState(SystemState.Running, true);
          // Do before importers are activated and after server is running so it is possible to use other parts of the client
          BackendExtension.MigrateDatabaseData();
          // To be done after default media item aspect types are present and when the system is running (other plugins might also install media item aspect types)
          BackendExtension.ActivateImporterWorker();

          SubscribeToMessages();
          UpdatePowerState();
        }
        catch (Exception e)
        {
          logger.Critical("Error starting application", e);
          _systemStateService.SwitchSystemState(SystemState.ShuttingDown, true);
          ServiceRegistration.IsShuttingDown = true;
          BackendExtension.DisposeBackendServices();
          ApplicationCore.DisposeCoreServices();
          _systemStateService.SwitchSystemState(SystemState.Ending, false);
          throw; // needed to cancel the start of the host
        }
      }
      catch (Exception ex)
      {
#if DEBUG
        ConsoleLogger log = new ConsoleLogger(LogLevel.All, false);
        log.Error(ex);
#else
        ServerCrashLogger crash = new ServerCrashLogger(logPath);
        crash.CreateLog(ex);
#endif
        _systemStateService.SwitchSystemState(SystemState.Ending, false);
        throw; // needed to cancel the start of the host
      }
    }

    /// <summary>
    /// Stops all services of the server.
    /// </summary>
    public void Stop()
    {
      try
      {
        UnsubscribeFromMessages();
        _systemStateService.SwitchSystemState(SystemState.ShuttingDown, true);
        ServiceRegistration.IsShuttingDown = true; // Block ServiceRegistration from trying to load new services in shutdown phase

        ServiceRegistration.Get<IImporterWorker>().Shutdown(); // needs to be shut down before MediaAccessor and plugins
        ServiceRegistration.Get<IMediaAccessor>().Shutdown();
        ServiceRegistration.Get<IPluginManager>().Shutdown();
        BackendExtension.ShutdownBackendServices();
        CloseIpc();
        ApplicationCore.StopCoreServices();
      }
      catch (Exception ex)
      {
#if DEBUG
        ConsoleLogger log = new ConsoleLogger(LogLevel.All, false);
        log.Error(ex);
#else
        var pathManager = ServiceRegistration.Get<IPathManager>();
        var logPath = pathManager.GetPath("<LOG>");
        ServerCrashLogger crash = new ServerCrashLogger(logPath);
        crash.CreateLog(ex);
#endif
        _systemStateService.SwitchSystemState(SystemState.ShuttingDown, true);
        ServiceRegistration.IsShuttingDown = true;
      }
      finally
      {
        BackendExtension.DisposeBackendServices();
        ApplicationCore.DisposeCoreServices();
        _systemStateService.SwitchSystemState(SystemState.Ending, false);
        _systemStateService.Dispose();
      }
    }

    #region IPC

    private void InitIpc()
    {
      if (_ipcServer != null || string.IsNullOrEmpty(_ipcName))
        return;
      ServiceRegistration.Get<ILogger>().Debug("Initializing IPC");
      try
      {
        _ipcServer = new IpcServer(_ipcName);
        _ipcServer.CustomShutdownCallback = _shutdownRequested;
        _ipcServer.Open();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error(ex);
      }
    }

    private void CloseIpc()
    {
      if (_ipcServer == null)
        return;
      try
      {
        _ipcServer.Close();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error(ex);
      }
      _ipcServer = null;
    }

    #endregion

    #region Message and power state handling

    private void SubscribeToMessages()
    {
      _messageQueue = new AsynchronousMessageQueue("ServerRuntime", new[] { ClientManagerMessaging.CHANNEL });
      _messageQueue.MessageReceived += OnMessageReceived;
      _messageQueue.Start();
    }

    private void UnsubscribeFromMessages()
    {
      if (_messageQueue == null)
        return;
      _messageQueue.Shutdown();
      _messageQueue = null;
    }

    private void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName != ClientManagerMessaging.CHANNEL)
        return;

      ClientManagerMessaging.MessageType messageType = (ClientManagerMessaging.MessageType) message.MessageType;
      switch (messageType)
      {
        case ClientManagerMessaging.MessageType.ClientOnline:
        case ClientManagerMessaging.MessageType.ClientOffline:
          UpdatePowerState();
          break;
      }
    }

    private void UpdatePowerState()
    {
      IClientManager clientManager = ServiceRegistration.Get<IClientManager>(false);
      if (clientManager == null)
        return;

      int countRemoteSystems = clientManager.ConnectedClients.
        Select(clientConnection => clientConnection.Descriptor.MPFrontendServerUUID).
        Count(mpFrontendServerUUID => !ServiceRegistration.Get<ISystemResolver>().GetSystemNameForSystemId(mpFrontendServerUUID).IsLocalSystem());

      // Avoid suspend as long as remote clients are connected. A local client prevents suspend using its own logic.
      // Set a continous state for current thread (which is the AMQ thread, not the "MainThread").
      _suspendLevel = countRemoteSystems > 0 ? SuspendLevel.AvoidSuspend : SuspendLevel.None;
      ServiceRegistration.Get<ILogger>().Debug("UpdatePowerState: Setting continuous suspend level to {0}", _suspendLevel);
      ServiceRegistration.Get<ISystemStateService>().SetCurrentSuspendLevel(_suspendLevel, true);
    }

    #endregion

#if !DEBUG
    private string GetDefaultLogPath()
    {
      // Used for crash logs before the path manager is available
      string dataDirectory = _dataDirectory;
      if (string.IsNullOrEmpty(dataDirectory))
        dataDirectory = Path.Combine(PathManager.GetStandardSpecialFolderMappings()["COMMON_APPLICATION_DATA"], "Team MediaPortal", "MP2-Server");
      return Path.Combine(dataDirectory, "Log");
    }
#endif
  }
}
