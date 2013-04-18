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
using System.IO;
using System.Net;
using System.Web;
using Griffin.Networking.Protocol.Http.Protocol;
using Griffin.WebServer;
using Griffin.WebServer.Modules;
using MediaPortal.Common;
using MediaPortal.Extensions.UserServices.FanArtService.Interfaces;

namespace MediaPortal.Extensions.UserServices.FanArtService
{
  public class FanartAccessModule : IWorkerModule
  {

    public void BeginRequest(IHttpContext context)
    {
    }

    public void EndRequest(IHttpContext context)
    {
    }

    public void HandleRequestAsync(IHttpContext context, Action<IAsyncModuleResult> callback)
    {
      // Since this module only supports sync
      callback(new AsyncModuleResult(context, HandleRequest(context)));
    }

    /// <summary>
    /// Method that process the url
    /// </summary>
    /// <param name="context">Information sent by the browser about the request</param>
    /// <returns>true if this module handled the request.</returns>
    public ModuleResult HandleRequest(IHttpContext context)
    {
      Uri uri = context.Request.Uri;
      if (!uri.AbsolutePath.StartsWith("/FanartService"))
        return ModuleResult.Continue;

      IFanArtService fanart = ServiceRegistration.Get<IFanArtService>(false);
      if (fanart == null)
        return ModuleResult.Stop;

      FanArtConstants.FanArtMediaType mediaType;
      FanArtConstants.FanArtType fanArtType;
      int maxWidth;
      int maxHeight;
      if (uri.Segments.Length < 4)
        return ModuleResult.Stop;
      if (!Enum.TryParse(GetSegmentWithoutSlash(uri, 2), out mediaType))
        return ModuleResult.Stop;
      if (!Enum.TryParse(GetSegmentWithoutSlash(uri, 3), out fanArtType))
        return ModuleResult.Stop;
      string name = GetSegmentWithoutSlash(uri, 4);

      // Both values are optional
      int.TryParse(GetSegmentWithoutSlash(uri, 5), out maxWidth);
      int.TryParse(GetSegmentWithoutSlash(uri, 6), out maxHeight);

      IList<FanArtImage> files = fanart.GetFanArt(mediaType, fanArtType, name, maxWidth, maxHeight, true);
      if (files == null || files.Count == 0)
        return ModuleResult.Stop;

      using (MemoryStream memoryStream = new MemoryStream(files[0].BinaryData))
        SendWholeStream(context.Response, memoryStream, false);
      return ModuleResult.Stop;
    }

    protected static string GetSegmentWithoutSlash(Uri uri, int index)
    {
      if (index >= uri.Segments.Length)
        return null;
      return HttpUtility.UrlDecode(uri.Segments[index].Replace("/", string.Empty));
    }

    protected void SendWholeStream(IResponse response, Stream resourceStream, bool onlyHeaders)
    {
      response.StatusCode = (int) HttpStatusCode.OK;
      response.ContentLength = (int) /* TODO: long support */ resourceStream.Length;

      if (onlyHeaders)
        return;

      Send(response, resourceStream, resourceStream.Length);
    }

    protected void Send(IResponse response, Stream resourceStream, long length)
    {
      const int BUF_LEN = 8192;
      byte[] buffer = new byte[BUF_LEN];
      int bytesRead;
      if (response.Body == null)
        response.Body = new MemoryStream();
      while ((bytesRead = resourceStream.Read(buffer, 0, length > BUF_LEN ? BUF_LEN : (int) length)) > 0) // Don't use Math.Min since (int) length is negative for length > Int32.MaxValue
      {
        length -= bytesRead;
        response.Body.Write(buffer, 0, bytesRead);
        if (response.Body.CanSeek)
          response.Body.Position = 0;
      }
    }
  }
}
