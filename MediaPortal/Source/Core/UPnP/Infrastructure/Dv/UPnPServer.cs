#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
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
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Griffin.WebServer;
using MediaPortal.Utilities.Exceptions;
using UPnP.Infrastructure.Dv.DeviceTree;
using UPnP.Infrastructure.Dv.GENA;
using UPnP.Infrastructure.Dv.SSDP;
using UPnP.Infrastructure.Utils;

namespace UPnP.Infrastructure.Dv
{
  /// <summary>
  /// Represents a container for all UPnP devices and services and provides the network functionality for the UPnP system.
  /// </summary>
  public class UPnPServer : IDisposable
  {
    /// <summary>
    /// Size of the queue which holds open HTTP requests before they are evaluated.
    /// </summary>
    public static int DEFAULT_HTTP_REQUEST_QUEUE_SIZE = 5;

    /// <summary>
    /// Prefix which is added to URLs for description documents.
    /// </summary>
    public static string DEFAULT_DESCRIPTION_URL_PREFIX = "/upnphost/description";

    /// <summary>
    /// Prefix which is added to URLs for control requests.
    /// </summary>
    public static string DEFAULT_CONTROL_URL_PREFIX = "/upnphost/control";

    /// <summary>
    /// Prefix which is added to URLs for event subsriptions.
    /// </summary>
    public static string DEFAULT_EVENT_SUB_URL_PREFIX = "/upnphost/eventing";

    protected ICollection<DvDevice> _rootDevices = new List<DvDevice>();
    protected ServerData _serverData = new ServerData();

    /// <summary>
    /// Creates a new UPnP server instance. After creating this instance, its root devices should be populated by calling
    /// <see cref="AddRootDevice"/> for each device.
    /// </summary>
    public UPnPServer()
    {
      _serverData.Server = this;
    }

    /// <summary>
    /// Disposes this <see cref="UPnPServer"/>, i.e. revokes UPnP network advertisements and closes all receiving servers.
    /// </summary>
    public virtual void Dispose()
    {
      Close();
      foreach (DvDevice rootDevice in _rootDevices)
        rootDevice.Dispose();
    }

    #region Event Handlers

    private void OnNetworkAddressChanged(object sender, EventArgs e)
    {
      lock (_serverData.SyncObj)
      {
        // To go around finding out which interface was changed, we simply raise an Update notification followed by a new advertisement
        UpdateInterfaceConfiguration();
        _serverData.SSDPController.Advertise();
      }
    }

    #endregion

    /// <summary>
    /// Returns the collection of root devices of this UPnP server.
    /// </summary>
    public ICollection<DvDevice> RootDevices
    {
      get { return _rootDevices; }
    }

    /// <summary>
    /// Adds a new UPnP root device. Should be done before <see cref="Bind"/> is called.
    /// </summary>
    /// <param name="device">Device to add to the <see cref="RootDevices"/> collection.</param>
    public void AddRootDevice(DvDevice device)
    {
      _rootDevices.Add(device);
    }

    /// <summary>
    /// Finds the device with the specified <paramref name="deviceUDN"/> in all device trees starting with the root devices.
    /// </summary>
    /// <param name="deviceUDN">Device UDN to search. The device UDN needs to be in the format "uuid:[device-UUID]"</param>
    /// <returns>UPnP device instance with the given <paramref name="deviceUDN"/> or <c>null</c>, if the specified device
    /// wasn't found in any of the root device trees.</returns>
    public DvDevice FindDeviceByUDN(string deviceUDN)
    {
      return _rootDevices.Select(rootDevice => rootDevice.FindDeviceByUDN(deviceUDN)).FirstOrDefault(result => result != null);
    }

    /// <summary>
    /// Finds all devices in all root device trees with the specified device <paramref name="type"/> and
    /// <paramref name="version"/>.
    /// </summary>
    /// <param name="type">Device type to search.</param>
    /// <param name="version">Version number of the device type to search.</param>
    /// <param name="searchCompatible">If set to <c>true</c>, this method also searches compatible devices,
    /// i.e. devices with a higher version number than requested.</param>
    public IEnumerable<DvDevice> FindDevicesByDeviceTypeAndVersion(string type, int version, bool searchCompatible)
    {
      return _rootDevices.SelectMany(rootDevice => rootDevice.FindDevicesByDeviceTypeAndVersion(type, version, searchCompatible));
    }

    /// <summary>
    /// Starts this UPnP server, i.e. starts a network listener and sends notifications about provided devices.
    /// </summary>
    /// <param name="advertisementInterval">Interval in seconds to repeat UPnP advertisements in the network.
    /// The UPnP architecture document (UPnP-arch-DeviceArchitecture-v1 1-20081015, 1.2.2, page 21) states a
    /// minimum of 1800 seconds. But in the world of today, that value is much to high for many applications and in many
    /// cases, a value of much less than 1800 seconds is choosen. For servers which will frequently change their
    /// availability, this value should be short, for more durable serves, this interval can be much longer (maybe a day).</param>
    public void Bind(int advertisementInterval = UPnPConsts.DEFAULT_ADVERTISEMENT_EXPIRATION_TIME)
    {
      lock (_serverData.SyncObj)
      {
        if (_serverData.IsActive)
          throw new IllegalCallException("UPnP subsystem mustn't be started multiple times");

        if (UPnPConfiguration.USE_IPV4)
        {
          var moduleManager = new ModuleManager();
          var upnpModule = new UPnPModule(_serverData);
          moduleManager.Add(upnpModule);
          try
          {
            var server = new HttpServer(moduleManager);
            server.Start(IPAddress.Any, 0); // Might fail if IPv4 isn't installed
            _serverData.HTTPListenerV4 = server;
            _serverData.HTTP_PORTv4 = ((IPEndPoint) _serverData.HTTPListenerV4.Server.Listener.LocalEndPoint).Port;
            UPnPConfiguration.LOGGER.Info("UPnP server: HTTP listener for IPv4 protocol started at port {0}", _serverData.HTTP_PORTv4);
          }
          catch (SocketException e)
          {
            _serverData.HTTPListenerV4 = null;
            _serverData.HTTP_PORTv4 = 0;
            UPnPConfiguration.LOGGER.Warn("UPnPServer: Error starting HTTP server (IPv4)", e);
          }
        }
        else
        {
          _serverData.HTTPListenerV4 = null;
          _serverData.HTTP_PORTv4 = 0;
          UPnPConfiguration.LOGGER.Info("UPnP server: IPv4 protocol disabled, so no HTTP listener started for IPv4");
        }

        if (UPnPConfiguration.USE_IPV6)
        {
          var moduleManager = new ModuleManager();
          var upnpModule = new UPnPModule(_serverData);
          moduleManager.Add(upnpModule);
          try
          {
            var server = new HttpServer(moduleManager);
            server.Start(IPAddress.IPv6Any, 0); // Might fail if IPv6 isn't installed
            _serverData.HTTPListenerV6 = server;
            _serverData.HTTP_PORTv6 = ((IPEndPoint) _serverData.HTTPListenerV6.Server.Listener.LocalEndPoint).Port;
            UPnPConfiguration.LOGGER.Info("UPnP server: HTTP listener for IPv6 protocol started at port {0}", _serverData.HTTP_PORTv6);
          }
          catch (SocketException e)
          {
            _serverData.HTTPListenerV6 = null;
            _serverData.HTTP_PORTv6 = 0;
            UPnPConfiguration.LOGGER.Warn("UPnPServer: Error starting HTTP server (IPv6)", e);
          }
        }
        else
        {
          _serverData.HTTPListenerV6 = null;
          _serverData.HTTP_PORTv6 = 0;
          UPnPConfiguration.LOGGER.Info("UPnP server: IPv6 protocol disabled, so no HTTP listener started for IPv6");
        }

        _serverData.SSDPController = new SSDPServerController(_serverData)
          {
              AdvertisementExpirationTime = advertisementInterval
          };
        _serverData.GENAController = new GENAServerController(_serverData);

        InitializeDiscoveryEndpoints();

        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        _serverData.IsActive = true;

        // At the end, start the controllers
        _serverData.SSDPController.Start();
        _serverData.GENAController.Start();
        UPnPConfiguration.LOGGER.Info("UPnP server online hosting {0} UPnP root devices", _serverData.Server.RootDevices.Count);
      }
    }

    /// <summary>
    /// Has to be called when the server's configuration (i.e. its devices, services, actions or state variables)
    /// was changed.
    /// </summary>
    /// <remarks>
    /// It is recommended not to change the server's configuration at runtime. Instead, the server's configuration
    /// should remain stable at runtime. Changes in the server's capabilities should be announced by appropriate
    /// state variables.
    /// </remarks>
    public void UpdateConfiguration()
    {
      lock (_serverData.SyncObj)
      {
        foreach (EndpointConfiguration config in _serverData.UPnPEndPoints)
        {
          GenerateObjectURLs(config);
          config.ConfigId = GenerateConfigId(config);
        }
        _serverData.SSDPController.Advertise();
      }
    }

    /// <summary>
    /// Removes all network components for the UPnP server.
    /// </summary>
    public void Close()
    {
      lock (_serverData.SyncObj)
      {
        if (!_serverData.IsActive)
          return;
        _serverData.IsActive = false;
      }
      _serverData.GENAController.Close();
      _serverData.SSDPController.Close();
      if (_serverData.HTTPListenerV4 != null)
        _serverData.HTTPListenerV4.Server.Stop();
      if (_serverData.HTTPListenerV6 != null)
        _serverData.HTTPListenerV6.Server.Stop();
      lock (_serverData.SyncObj)
        _serverData.UPnPEndPoints.Clear();
    }

    #region Protected methods

    protected void GenerateObjectURLs(EndpointConfiguration config)
    {
      DeviceTreeURLGenerator.GenerateObjectURLs(this, config);
    }

    protected Int32 GenerateConfigId(EndpointConfiguration config)
    {
      Int64 result = config.RootDeviceDescriptionPathsToRootDevices.Values.Select(
          rootDevice => rootDevice.BuildRootDeviceDescription(
              _serverData, config, CultureInfo.InvariantCulture)).Aggregate<string, long>(
                  0, (current, description) => current + HashGenerator.CalculateHash(0, description));
      result = config.SCPDPathsToServices.Values.Select(service => service.BuildSCPDDocument(
          config, _serverData)).Aggregate(result, (current, description) => current + HashGenerator.CalculateHash(0, description));
      result += HashGenerator.CalculateHash(0, NetworkHelper.IPAddrToString(config.EndPointIPAddress));
      result += config.HTTPServerPort;
      result += HashGenerator.CalculateHash(0, config.ControlPathBase + config.DescriptionPathBase + config.EventSubPathBase);
      return (int) result;
    }

    protected void UpdateInterfaceConfiguration()
    {
      InitializeDiscoveryEndpoints();

      _serverData.SSDPController.Update();
    }

    protected void InitializeDiscoveryEndpoints()
    {
      IDictionary<IPAddress, EndpointConfiguration> oldEndpoints = new Dictionary<IPAddress, EndpointConfiguration>();
      foreach (EndpointConfiguration config in _serverData.UPnPEndPoints)
        oldEndpoints.Add(config.EndPointIPAddress, config);
      IList<IPAddress> addresses = NetworkHelper.OrderAddressesByScope(NetworkHelper.GetUPnPEnabledIPAddresses());

      // Add new endpoints
      foreach (IPAddress address in addresses)
      {
        if (oldEndpoints.ContainsKey(address))
          continue;
        AddressFamily family = address.AddressFamily;
        if (family == AddressFamily.InterNetwork && !UPnPConfiguration.USE_IPV4)
          continue;
        if (family == AddressFamily.InterNetworkV6 && !UPnPConfiguration.USE_IPV6)
          continue;

        UPnPConfiguration.LOGGER.Debug("UPnPServer: Initializing IP endpoint '{0}'", NetworkHelper.IPAddrToString(address));
        EndpointConfiguration config = new EndpointConfiguration
          {
              EndPointIPAddress = address,
              DescriptionPathBase = DEFAULT_DESCRIPTION_URL_PREFIX,
              ControlPathBase = DEFAULT_CONTROL_URL_PREFIX,
              EventSubPathBase = DEFAULT_EVENT_SUB_URL_PREFIX,
              HTTPServerPort = family == AddressFamily.InterNetwork ? _serverData.HTTP_PORTv4 : _serverData.HTTP_PORTv6
          };
        GenerateObjectURLs(config);
        config.ConfigId = GenerateConfigId(config);
        _serverData.UPnPEndPoints.Add(config);
        _serverData.SSDPController.StartSSDPEndpoint(config);
        _serverData.GENAController.InitializeGENAEndpoint(config);
      }
      // Remove obsolete endpoints
      foreach (EndpointConfiguration config in new List<EndpointConfiguration>(_serverData.UPnPEndPoints))
        if (!addresses.Contains(config.EndPointIPAddress))
        {
          UPnPConfiguration.LOGGER.Debug("UPnPServer: Removing obsolete IP endpoint IP '{0}'", NetworkHelper.IPAddrToString(config.EndPointIPAddress));
          _serverData.GENAController.CloseGENAEndpoint(config);
          _serverData.SSDPController.CloseSSDPEndpoint(config, false);
          _serverData.UPnPEndPoints.Remove(config);
        }
    }

    #endregion
  }
}
