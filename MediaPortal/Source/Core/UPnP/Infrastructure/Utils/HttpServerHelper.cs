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

using System.Net;
using Griffin.Networking.Protocol.Http.Protocol;

namespace UPnP.Infrastructure.Utils
{
  public static class HttpServerHelper
  {
    /// <summary>
    /// Given an HTTP request, this method returns the client's IP address.
    /// </summary>
    /// <param name="request">Http client request.</param>
    /// <returns><see cref="string"/> instance containing the client's IP address. The returned IP address can be
    /// parsed by calling <see cref="IPAddress.Parse"/>.</returns>
    public static string GetRemoteAddress(IRequest request)
    {
      return request.GetHeader("remote_addr");
    }

    /// <summary>
    /// Extension method to safely return a header entry with name <paramref name="header"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="header">Name of header.</param>
    /// <returns>Header value or <c>string.Empty</c> if missing.</returns>
    public static string GetHeader(this IRequest request, string header)
    {
      IHeaderItem item = request.Headers["remote_addr"];
      return item != null ? item.Value : string.Empty;
    }
  }
}