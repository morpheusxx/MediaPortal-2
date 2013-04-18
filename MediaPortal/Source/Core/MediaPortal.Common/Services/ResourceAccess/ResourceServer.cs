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
using System.Net;
using System.Net.Sockets;
using Griffin.WebServer;
using Griffin.WebServer.Modules;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess.Settings;
using MediaPortal.Common.Settings;

namespace MediaPortal.Common.Services.ResourceAccess
{
  public class ResourceServer : IResourceServer, IDisposable
  {
    //internal class HttpLogWriter : ILogWriter
    //{
    //  public void Write(object source, LogPrio priority, string message)
    //  {
    //    string msg = source + ": " + message;
    //    ILogger logger = ServiceRegistration.Get<ILogger>();
    //    switch (priority)
    //    {
    //      case LogPrio.Trace:
    //        // Don't write trace messages (we don't support a trace level in MP - would have to map it to debug level)
    //        break;
    //      case LogPrio.Debug:
    //        logger.Debug(msg);
    //        break;
    //      case LogPrio.Info:
    //        logger.Info(msg);
    //        break;
    //      case LogPrio.Warning:
    //        logger.Warn(msg);
    //        break;
    //      case LogPrio.Error:
    //        logger.Error(msg);
    //        break;
    //      case LogPrio.Fatal:
    //        logger.Critical(msg);
    //        break;
    //    }
    //  }
    //}

    protected readonly HttpServer _httpServerV4;
    protected readonly HttpServer _httpServerV6;
    protected readonly ModuleManager _moduleManagerV4;
    protected readonly ModuleManager _moduleManagerV6;

    public ResourceServer()
    {
      _moduleManagerV4 = new ModuleManager();
      _moduleManagerV6 = new ModuleManager();
      _httpServerV4 = new HttpServer(_moduleManagerV4); //new HttpLogWriter()
      _httpServerV6 = new HttpServer(_moduleManagerV6);
      ResourceAccessModule module = new ResourceAccessModule();
      AddHttpModule(module);
    }

    public void Dispose()
    {
      StopServers();
      DisposeServers();
    }

    public void StartServers()
    {
      ServerSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<ServerSettings>();
      if (settings.UseIPv4)
        try
        {
          _httpServerV4.Start(IPAddress.Any, settings.HttpServerPort);
          ServiceRegistration.Get<ILogger>().Info("ResourceServer: Started HTTP server (IPv4) at port {0}", PortIPv4);
        }
        catch (SocketException e)
        {
          ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error starting HTTP server (IPv4)", e);
        }
      if (settings.UseIPv6)
        try
        {
          _httpServerV6.Start(IPAddress.IPv6Any, settings.HttpServerPort);
          ServiceRegistration.Get<ILogger>().Info("ResourceServer: Started HTTP server (IPv6) at port {0}", PortIPv6);
        }
        catch (SocketException e)
        {
          ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error starting HTTP server (IPv6)", e);
        }
    }

    public void StopServers()
    {
      try
      {
        _httpServerV4.Stop();
      }
      catch (SocketException e)
      {
        ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error stopping HTTP server (IPv4)", e);
      }
      try
      {
        _httpServerV6.Stop();
      }
      catch (SocketException e)
      {
        ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error stopping HTTP server (IPv6)", e);
      }
    }

    public void DisposeServers()
    {
      //try
      //{
      //  _httpServerV4.Server.Stop();
      //}
      //catch (SocketException e)
      //{
      //  ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error stopping HTTP server (IPv4)", e);
      //}
      //try
      //{
      //  _httpServerV6.Server.Stop();
      //}
      //catch (SocketException e)
      //{
      //  ServiceRegistration.Get<ILogger>().Warn("ResourceServer: Error stopping HTTP server (IPv6)", e);
      //}
    }

    #region IResourceServer implementation

    public int PortIPv4
    {
      get { return ((IPEndPoint) _httpServerV4.Server.Listener.LocalEndPoint).Port; }
    }

    public int PortIPv6
    {
      get { return ((IPEndPoint) _httpServerV6.Server.Listener.LocalEndPoint).Port; }
    }

    public void Startup()
    {
      StartServers();
    }

    public void Shutdown()
    {
      ServiceRegistration.Get<ILogger>().Info("ResourceServer: Shutting down HTTP servers");
      StopServers();
    }

    public void RestartHttpServers()
    {
      ServiceRegistration.Get<ILogger>().Info("ResourceServer: Restarting HTTP servers");
      StopServers();
      StartServers();
    }

    public void AddHttpModule(IWorkerModule module)
    {
      _moduleManagerV4.Add(module);
      _moduleManagerV6.Add(module);
    }

    public void RemoveHttpModule(IWorkerModule module)
    {
      // TODO:
      // _moduleManagerV4.Remove(module);
      // _moduleManagerV6.Remove(module);
    }

    #endregion
  }
}