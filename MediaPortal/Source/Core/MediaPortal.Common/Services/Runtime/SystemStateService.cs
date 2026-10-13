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
using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using MediaPortal.Common.Logging;
using MediaPortal.Common.Runtime;
using MediaPortal.Utilities.SystemAPI;

namespace MediaPortal.Common.Services.Runtime
{
  public class SystemStateService : ISystemStateService, IDisposable
  {
    protected SystemState _state = SystemState.Starting;
    protected IDisposable _powerEventHandler;
    protected SuspendLevel _lastContinuousSuspendLevel = SuspendLevel.None;

    public SystemStateService()
    {
      // Power events (suspend/resume notifications) are currently only available on Windows
      if (OperatingSystem.IsWindows())
        _powerEventHandler = CreatePowerEventHandler();
    }

    [SupportedOSPlatform("windows")]
    private PowerEventHandler CreatePowerEventHandler()
    {
      PowerEventHandler handler = new PowerEventHandler();
      handler.OnPowerEvent += SendSystemStatePowerMessage;
      handler.OnQuerySuspend += OnQuerySuspend;
      return handler;
    }

    /// <summary>
    /// Runs <c>systemctl</c> with the given <paramref name="command"/> on non-Windows systems.
    /// </summary>
    protected static void SystemCtl(string command)
    {
      try
      {
        Process.Start(new ProcessStartInfo("systemctl", command) { UseShellExecute = false });
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SystemStateService: Failed to execute 'systemctl {0}'", ex, command);
      }
    }

    public void SwitchSystemState(SystemState newState, bool sendMessage)
    {
      _state = newState;
      if (sendMessage)
        SystemMessaging.SendSystemStateChangeMessage(_state);
    }

    #region ISystemStateService implementation

    public SystemState CurrentState
    {
      get { return _state; }
    }

    public void Shutdown(bool force = false)
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Shutting down");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.ShuttingDown);

      if (!OperatingSystem.IsWindows())
      {
        SystemCtl("poweroff");
        return;
      }

      WindowsAPI.EXIT_WINDOWS flags = WindowsAPI.EXIT_WINDOWS.EWX_POWEROFF;
      if (force)
        flags |= WindowsAPI.EXIT_WINDOWS.EWX_FORCE;

      // todo: chefkoch, 2013-01-31: add flag for HybridShutdown if OS is Windows 8

      WindowsAPI.ExitWindowsEx(flags);
    }

    public void Restart(bool force = false)
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Restarting");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.ShuttingDown);

      if (!OperatingSystem.IsWindows())
      {
        SystemCtl("reboot");
        return;
      }

      WindowsAPI.EXIT_WINDOWS flags = WindowsAPI.EXIT_WINDOWS.EWX_REBOOT;
      if (force)
        flags |= WindowsAPI.EXIT_WINDOWS.EWX_FORCE;

      WindowsAPI.ExitWindowsEx(flags);
    }

    public void Suspend()
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Suspending");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.Suspending);

      if (!OperatingSystem.IsWindows())
      {
        SystemCtl("suspend");
        return;
      }

      WindowsAPI.SetSuspendState(false, false, false);
    }

    public void Hibernate()
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Hibernating");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.Hibernating);

      if (!OperatingSystem.IsWindows())
      {
        SystemCtl("hibernate");
        return;
      }

      WindowsAPI.SetSuspendState(true, false, false);
    }

    public void Logoff(bool force = false)
    {
      ServiceRegistration.Get<ILogger>().Info("SystemStateService: Logging off");
      SystemMessaging.SendSystemStateChangeMessage(SystemState.ShuttingDown);

      if (!OperatingSystem.IsWindows())
      {
        ServiceRegistration.Get<ILogger>().Warn("SystemStateService: Logoff is not supported on this platform");
        return;
      }

      WindowsAPI.EXIT_WINDOWS flags = WindowsAPI.EXIT_WINDOWS.EWX_LOGOFF;
      if (force)
        flags |= WindowsAPI.EXIT_WINDOWS.EWX_FORCE;

      WindowsAPI.ExitWindowsEx(flags);
    }

    [SupportedOSPlatform("windows")]
    protected void SendSystemStatePowerMessage(PowerBroadcastStatus status)
    {
      switch (status)
      {
        case PowerBroadcastStatus.ResumeAutomatic:
        case PowerBroadcastStatus.ResumeCritical:
        case PowerBroadcastStatus.ResumeSuspend:
          SystemMessaging.SendSystemStateChangeMessage(SystemState.Resuming);
          break;
        case PowerBroadcastStatus.Suspend:
          SystemMessaging.SendSystemStateChangeMessage(SystemState.Suspending);
          break;
      }
    }

    public void SetCurrentSuspendLevel(SuspendLevel level, bool continuous = false)
    {
      WindowsAPI.EXECUTION_STATE requestedState = 0;
      switch (level)
      {
        case SuspendLevel.AvoidSuspend:
          requestedState = WindowsAPI.EXECUTION_STATE.ES_SYSTEM_REQUIRED;
          break;
        case SuspendLevel.DisplayRequired:
          requestedState = WindowsAPI.EXECUTION_STATE.ES_DISPLAY_REQUIRED;
          break;
      }

      if (continuous)
      {
        _lastContinuousSuspendLevel = level; // Remember the value to handle OnQuerySuspend properly
        requestedState |= WindowsAPI.EXECUTION_STATE.ES_CONTINUOUS;
      }

      WindowsAPI.SetThreadExecutionState(requestedState);
    }

    protected bool OnQuerySuspend()
    {
      // Deny suspend requests as long as a continuous level of "AvoidSuspend" or "DisplayRequired" is set.
      // "true" allows standby, "false" denies it.
      return _lastContinuousSuspendLevel == SuspendLevel.None;
    }

    #endregion

    public void Dispose()
    {
      _powerEventHandler?.Dispose();
    }
  }
}