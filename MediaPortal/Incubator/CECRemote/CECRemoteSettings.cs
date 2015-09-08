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

using MediaPortal.Common.Settings;
using System.Collections.Generic;
using MediaPortal.Common.Configuration.ConfigurationClasses;

namespace MediaPortal.UiComponents.CECRemote.Settings
{
  public class HDMIport : LimitedNumberSelect
  {
    public override void Load()
    {
      _type = NumberType.Integer;
      _step = 1;
      _lowerLimit = 1;
      _upperLimit = 10;
      _value = SettingsManager.Load<CECRemoteSettings>().HDMIPort;
    }

    public override void Save()
    {
      CECRemoteSettings settings = SettingsManager.Load<CECRemoteSettings>();
      settings.HDMIPort = (int)_value;
      SettingsManager.Save(settings);
    }
  }
  /// <summary>
  /// Settings class for CECRemotePlugin.
  /// </summary>
  public class CECRemoteSettings
  {
    #region Variables

    protected int _hdmiPort;
    protected List<MappedKeyCode> _remoteMap;

    #endregion Variables

    #region Properties

    /// <summary>
    /// Gets or sets the HDMI port.
    /// </summary>
    [Setting(SettingScope.Global, 1)]
    public int HDMIPort
    {
      get { return _hdmiPort; }
      set { _hdmiPort = value; }
    }

    /// <summary>
    /// Gets or sets the remote map.
    /// </summary>
    [Setting(SettingScope.User)]
    public ICollection<MappedKeyCode> RemoteMap
    {
      get { return _remoteMap; }
    }

    #endregion Properties

    #region Additional members for the XML serialization

    public List<MappedKeyCode> XML_RemoteMap
    {
      get { return _remoteMap; }
      set { _remoteMap = value; }
    }

    #endregion
  }
}
