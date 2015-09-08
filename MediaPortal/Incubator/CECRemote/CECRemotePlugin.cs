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

using CecSharp;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UiComponents.CECRemote.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Serialization;

namespace MediaPortal.UiComponents.CECRemote
{
  /// <summary>
  /// Input Service plugin. Communicates with Pulse-Eight CEC adapter.
  /// </summary>
  public class CECRemotePlugin : IPluginStateTracker
  {
    #region Variables

    private CecClient _client;
    private DateTime _keyTimeStamp;
    private IInputManager _inputManager;
    private bool _keyDown;
    private IDictionary<string, Key> _mappedKeyCodes;

    #endregion

    #region Event handlers

    private void CecRemoteKeyEvent(object sender, CecRemoteEventArgs e)
    {
      RemoteHandler(e.Key.Keycode.ToString());
    }

    private void CecRemoteCommandEvent(object sender, CecRemoteEventArgs e)
    {
      if (e.Command.Opcode == CecOpcode.UserControlPressed)
        _keyDown = true;
      else if (e.Command.Opcode == CecOpcode.UserControlRelease)
        _keyDown = false;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Connects the CEC client.
    /// </summary>    
    private void StartClient()
    {
      if (_client != null)
        return;

      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      CECRemoteSettings settings = settingsManager.Load<CECRemoteSettings>();

      _client = new CecClient((byte)settings.HDMIPort, "HTPC", CecDeviceType.RecordingDevice, CecLogLevel.Error);
      _client.CecRemoteCommandEvent += CecRemoteCommandEvent;
      _client.CecRemoteKeyEvent += CecRemoteKeyEvent;

      if (_client.Connect(10000))
      {
        ServiceRegistration.Get<ILogger>().Info("CECRemotePlugin: Connect to HDMI port ({0})", settings.HDMIPort);
        return;
      }
      StopClient();
    }

    /// <summary>
    /// Stops the CEC client.
    /// </summary>
    private void StopClient()
    {
      if (_client == null)
        return;

      _client.CecRemoteCommandEvent -= CecRemoteCommandEvent;
      _client.CecRemoteKeyEvent -= CecRemoteKeyEvent;
      _client.Close();
      _client.Dispose();
      _client = null;
    }

    private void RemoteHandler(string remoteButton)
    {
      DateTime current = DateTime.Now;
      TimeSpan span = current.Subtract(_keyTimeStamp);

      if (span.TotalMilliseconds < 160 || _keyDown == false)
        return;

      _keyTimeStamp = current;

      if (_inputManager == null)
      {
        _inputManager = ServiceRegistration.Get<IInputManager>();

        if (_inputManager == null)
        {
          ServiceRegistration.Get<ILogger>().Error("CECRemotePlugin: No InputManager, can't map and act on '{0}'", remoteButton);
          return;
        }
      }

      Key key;
      if (_mappedKeyCodes.TryGetValue(remoteButton, out key))
      {
        _inputManager.KeyPress(key);
        ServiceRegistration.Get<ILogger>().Debug("CECRemotePlugin: Mapped Key '{0}' to '{1}'", remoteButton, key);
      }
      else
        ServiceRegistration.Get<ILogger>().Warn("CECRemotePlugin: No remote mapping found for remote button '{0}'", remoteButton);
    }

    private ICollection<MappedKeyCode> LoadRemoteMap(string remoteFile)
    {
      XmlSerializer reader = new XmlSerializer(typeof(List<MappedKeyCode>));
      using (StreamReader file = new StreamReader(remoteFile))
        return (ICollection<MappedKeyCode>)reader.Deserialize(file);
    }

    private void SaveRemoteMap(string remoteFile, ICollection<MappedKeyCode> remoteMap)
    {
      XmlSerializer writer = new XmlSerializer(typeof(List<MappedKeyCode>));
      using (StreamWriter file = new StreamWriter(remoteFile))
        writer.Serialize(file, remoteMap);
    }

    #endregion

    #region IPluginStateTracker implementation

    public void Activated(PluginRuntime pluginRuntime)
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      // We initialize the key code map here instead of in the constructor because here, we have access to the plugin's
      // directory (via the pluginRuntime parameter).
      CECRemoteSettings settings = settingsManager.Load<CECRemoteSettings>();
      _mappedKeyCodes = new Dictionary<string, Key>();
      ICollection<MappedKeyCode> keyCodes = settings.RemoteMap ??
          LoadRemoteMap(pluginRuntime.Metadata.GetAbsolutePath("DefaultRemoteMap.xml"));

      foreach (MappedKeyCode mkc in keyCodes)
        _mappedKeyCodes.Add(mkc.Code, mkc.Key);

      Thread startupThread = new Thread(StartClient)
        {
          IsBackground = true,
          Priority = ThreadPriority.BelowNormal,
          Name = "CECRemote"
        };

      startupThread.Start();
    }

    public bool RequestEnd()
    {
      return true;
    }

    public void Stop()
    {
      StopClient();
    }

    public void Continue() { }

    public void Shutdown()
    {
      StopClient();
    }

    #endregion
  }
}
