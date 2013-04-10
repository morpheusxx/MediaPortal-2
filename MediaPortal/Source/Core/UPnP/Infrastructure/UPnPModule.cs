using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Griffin.Networking.Protocol.Http.Protocol;
using Griffin.WebServer;
using Griffin.WebServer.Modules;
using UPnP.Infrastructure.Dv;
using UPnP.Infrastructure.Dv.DeviceTree;
using UPnP.Infrastructure.Dv.GENA;
using UPnP.Infrastructure.Dv.SOAP;
using UPnP.Infrastructure.Utils;

namespace UPnP.Infrastructure
{
  public class UPnPModule : IWorkerModule
  {
    protected ServerData _serverData;

    public UPnPModule(ServerData serverData)
    {
      _serverData = serverData;
    }

    /// <summary>
    /// Handles all kinds of HTTP over TCP requests - Description, Control and Event subscriptions.
    /// </summary>
    /// <param name="context">HTTP client context of the current request.</param>
    //// <param name="request">HTTP request to handle.</param>
    //protected void HandleHTTPRequest_NoLock(IHttpClientContext context, IHttpRequest request)
    public ModuleResult HandleRequest(IHttpContext context)
    {
      IRequest request = context.Request;
      Uri uri = context.Request.Uri;
      string hostName = uri.Host;
      string pathAndQuery = uri.LocalPath;
      // Unfortunately, Uri.PathAndQuery doesn't decode characters like '{' and '}', so we use the Uri.LocalPath property
      try
      {
        DvService service;
        ICollection<EndpointConfiguration> endpoints;
        lock (_serverData.SyncObj)
          endpoints = _serverData.UPnPEndPoints;
        foreach (EndpointConfiguration config in endpoints)
        {
          if (!NetworkHelper.HostNamesEqual(hostName, NetworkHelper.IPAddrToHostName(config.EndPointIPAddress)))
            continue;

          // Common check for supported encodings
          string acceptEncoding = request.Headers["ACCEPT-ENCODING"].Value ?? string.Empty;

          // Handle different HTTP methods here
          if (request.Method == "GET")
          {
            // GET of descriptions
            if (pathAndQuery.StartsWith(config.DescriptionPathBase))
            {
              string acceptLanguage = request.Headers["ACCEPT-LANGUAGE"].Value;
              CultureInfo culture = GetFirstCultureOrDefault(acceptLanguage, CultureInfo.InvariantCulture);

              string description = null;
              DvDevice rootDevice;
              lock (_serverData.SyncObj)
                if (config.RootDeviceDescriptionPathsToRootDevices.TryGetValue(pathAndQuery, out rootDevice))
                  description = rootDevice.BuildRootDeviceDescription(_serverData, config, culture);
                else if (config.SCPDPathsToServices.TryGetValue(pathAndQuery, out service))
                  description = service.BuildSCPDDocument(config, _serverData);
              if (description != null)
              {
                context.Response.StatusCode = (int) HttpStatusCode.OK;
                context.Response.ContentType = "text/xml; charset=utf-8";
                if (!string.IsNullOrEmpty(acceptLanguage))
                  context.Response.AddHeader("CONTENT-LANGUAGE", culture.ToString());
                using (MemoryStream responseStream = new MemoryStream(UPnPConsts.UTF8_NO_BOM.GetBytes(description)))
                  CompressionHelper.WriteCompressedStream(acceptEncoding, context.Response, responseStream);
                return ModuleResult.Stop;
              }
            }
          }
          else if (request.Method == "POST")
          {
            // POST of control messages
            if (config.ControlPathsToServices.TryGetValue(pathAndQuery, out service))
            {
              string contentType = request.Headers["CONTENT-TYPE"].Value;
              string userAgentStr = request.Headers["USER-AGENT"].Value;
              int minorVersion;
              if (string.IsNullOrEmpty(userAgentStr))
                minorVersion = 0;
              else if (!ParserHelper.ParseUserAgentUPnP1MinorVersion(userAgentStr, out minorVersion))
              {
                context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                return ModuleResult.Stop;
              }
              string mediaType;
              Encoding encoding;
              if (!EncodingUtils.TryParseContentTypeEncoding(contentType, Encoding.UTF8, out mediaType, out encoding))
                throw new ArgumentException("Unable to parse content type");
              if (mediaType != "text/xml")
              {
                // As specified in (DevArch), 3.2.1
                context.Response.StatusCode = (int) HttpStatusCode.UnsupportedMediaType;
                return ModuleResult.Stop;
              }
              context.Response.AddHeader("DATE", DateTime.Now.ToUniversalTime().ToString("R"));
              context.Response.AddHeader("SERVER", UPnPConfiguration.UPnPMachineInfoHeader);
              context.Response.AddHeader("CONTENT-TYPE", "text/xml; charset=\"utf-8\"");
              string result;
              HttpStatusCode status;
              try
              {
                CallContext callContext = new CallContext(request, context, config);
                status = SOAPHandler.HandleRequest(service, request.Body, encoding, minorVersion >= 1, callContext,
                  out result);
              }
              catch (Exception e)
              {
                UPnPConfiguration.LOGGER.Warn("Action invocation failed", e);
                result = SOAPHandler.CreateFaultDocument(501, "Action failed");
                status = HttpStatusCode.InternalServerError;
              }
              context.Response.StatusCode = (int) status;
              using (MemoryStream responseStream = new MemoryStream(encoding.GetBytes(result)))
                CompressionHelper.WriteCompressedStream(acceptEncoding, context.Response, responseStream);
              return ModuleResult.Stop;
            }
          }
          else if (request.Method == "SUBSCRIBE" || request.Method == "UNSUBSCRIBE")
          {
            GENAServerController gsc;
            lock (_serverData.SyncObj)
              gsc = _serverData.GENAController;
            if (gsc.HandleHTTPRequest(context.Request, context, config))
              return ModuleResult.Stop;
          }
          else
          {
            context.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
            return ModuleResult.Stop;
          }
        }
        // Url didn't match
        context.Response.StatusCode = (int) HttpStatusCode.NotFound;
        return ModuleResult.Stop;
      }
      catch (Exception e)
      {
        UPnPConfiguration.LOGGER.Error("UPnPServer: Error handling HTTP request '{0}'", e, uri);
        context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
        return ModuleResult.Stop;
      }
    }

    public void BeginRequest(IHttpContext context)
    {
    }

    public void EndRequest(IHttpContext context)
    {
    }

    public void HandleRequestAsync(IHttpContext context, Action<IAsyncModuleResult> callback)
    {
    }
    
    private static CultureInfo GetFirstCultureOrDefault(string cultureList, CultureInfo defaultCulture)
    {
      if (string.IsNullOrEmpty(cultureList))
        return defaultCulture;
      int index = cultureList.IndexOf(',');
      if (index > -1)
        try
        {
          return CultureInfo.GetCultureInfo(cultureList.Substring(0, index));
        }
        catch (ArgumentException)
        {
        }
      return defaultCulture;
    }
  }
}
