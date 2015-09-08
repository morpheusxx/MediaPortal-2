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
using CecSharp;

namespace MediaPortal.UiComponents.CECRemote
{
  public class CecRemoteEventArgs : EventArgs
  {
    private readonly CecKeypress _key;
    private readonly CecLogMessage _message;
    private readonly CecCommand _command;

    public CecRemoteEventArgs(CecKeypress key)
    {
      _key = key;
    }

    public CecRemoteEventArgs(CecLogMessage message)
    {
      _message = message;
    }

    public CecRemoteEventArgs(CecCommand command)
    {
      _command = command;
    }

    public CecKeypress Key
    {
      get { return _key; }
    }

    public CecLogMessage Message
    {
      get { return _message; }
    }

    public CecCommand Command
    {
      get { return _command; }
    }
  }

  public delegate void CecRemoteKeyEventHandler(object sender, CecRemoteEventArgs e);
  public delegate void CecRemoteLogEventHandler(object sender, CecRemoteEventArgs e);
  public delegate void CecRemoteCommandEventHandler(object sender, CecRemoteEventArgs e);

  class CecClient : CecCallbackMethods
  {
    private int _logLevel;
    private LibCecSharp _lib;
    private LibCECConfiguration _config;

    public event CecRemoteKeyEventHandler CecRemoteKeyEvent;
    public event CecRemoteLogEventHandler CecRemoteLogEvent;
    public event CecRemoteCommandEventHandler CecRemoteCommandEvent;

    public CecClient(byte hdmiPort, string deviceName, CecDeviceType deviceType, CecLogLevel level)
    {
      if (_config == null)
      {
        _config = new LibCECConfiguration();
        _config.SetCallbacks(this);
      }
      _config.DeviceTypes.Types[0] = deviceType;
      _config.DeviceName = deviceName;
      _config.ClientVersion = CecClientVersion.Version2_1_3;
      _config.AutodetectAddress = true;
      _config.PhysicalAddress = 0;
      _config.HDMIPort = hdmiPort;
      _logLevel = (int)level;

      _lib = new LibCecSharp(_config);

    }

    protected virtual void OnCecRemoteKeyEvent(CecRemoteEventArgs e)
    {
      if (CecRemoteKeyEvent != null)
      {
        CecRemoteKeyEvent(this, e);
      }
    }

    protected virtual void OnCecRemoteLogEvent(CecRemoteEventArgs e)
    {
      if (CecRemoteLogEvent != null)
      {
        CecRemoteLogEvent(this, e);
      }
    }

    protected virtual void OnCecRemoteCommandEvent(CecRemoteEventArgs e)
    {
      if (CecRemoteCommandEvent != null)
      {
        CecRemoteCommandEvent(this, e);
      }
    }

    public override int ReceiveCommand(CecCommand command)
    {
      //test fix for samsung play/stop keys
      if (command.Opcode == CecOpcode.Play || command.Opcode == CecOpcode.DeckControl)
      {
        CecKeypress key = new CecKeypress();
        key.Duration = 0;
        if (command.Opcode == CecOpcode.Play)
        {
          key.Keycode = CecUserControlCode.Play;
        }
        else
        {
          key.Keycode = CecUserControlCode.Stop;
        }

        CecRemoteEventArgs e = new CecRemoteEventArgs(key);
        OnCecRemoteKeyEvent(e);
      }
      else
      {
        CecRemoteEventArgs e = new CecRemoteEventArgs(command);
        OnCecRemoteCommandEvent(e);
      }
      return 1;
    }

    public override int ReceiveKeypress(CecKeypress key)
    {
      CecRemoteEventArgs e = new CecRemoteEventArgs(key);
      OnCecRemoteKeyEvent(e);

      return 1;
    }

    public override int ReceiveLogMessage(CecLogMessage message)
    {
      if ((int)message.Level <= _logLevel)
      {
        CecRemoteEventArgs e = new CecRemoteEventArgs(message);
        OnCecRemoteLogEvent(e);
      }
      return 1;
    }

    public override int ConfigurationChanged(LibCECConfiguration _config)
    {
      return 1;
    }


    public bool Connect(int timeout)
    {
      CecAdapter[] adapters = _lib.FindAdapters(string.Empty);
      if (adapters.Length > 0)
      {
        return Connect(adapters[0].ComPort, timeout);
      }
      else
        return false;
    }

    public bool Connect(string port, int timeout)
    {
      return _lib.Open(port, timeout);
    }

    public void Close()
    {
      _lib.DisableCallbacks();
      _lib.Close();
      _lib.Dispose();

      _lib = null;
      _config = null;
    }
  }
}
