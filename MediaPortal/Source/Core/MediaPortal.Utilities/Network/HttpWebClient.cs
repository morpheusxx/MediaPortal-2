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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaPortal.Utilities.Network
{
  /// <summary>
  /// Exception for a response of a web server with an error status code. The message has the format of the message of the former
  /// <c>WebClient</c> ("The remote server returned an error: (404) Not Found.").
  /// </summary>
  public class HttpStatusWebException : WebException
  {
    public HttpStatusWebException(HttpStatusCode statusCode, string reasonPhrase)
      : base(string.Format("The remote server returned an error: ({0}) {1}.", (int) statusCode, reasonPhrase), null, WebExceptionStatus.ProtocolError, null)
    {
      StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public HttpStatusCode StatusCode { get; private set; }
  }

  /// <summary>
  /// Simple client to download data from web servers. It replaces the obsolete <c>WebClient</c> and is based on <see cref="HttpClient"/>.
  /// Compressed responses (gzip, deflate) are decompressed automatically if <see cref="EnableCompression"/> is set.
  /// </summary>
  /// <remarks>
  /// Failures are reported as <see cref="WebException"/> like it was done by the <c>WebClient</c>: <see cref="HttpStatusWebException"/> if the server
  /// answered with an error status code, <see cref="WebExceptionStatus.Timeout"/> if the server doesn't answer in time (<see cref="RequestTimeout"/>)
  /// and <see cref="WebExceptionStatus.ConnectFailure"/> if there is no connection.
  /// </remarks>
  public class HttpWebClient : IDisposable
  {
    private static readonly HttpClient COMPRESSED_CLIENT = CreateClient(true);
    private static readonly HttpClient PLAIN_CLIENT = CreateClient(false);

    private static HttpClient CreateClient(bool decompress)
    {
      SocketsHttpHandler handler = new SocketsHttpHandler
        {
          AutomaticDecompression = decompress ? DecompressionMethods.GZip | DecompressionMethods.Deflate : DecompressionMethods.None,
          ConnectTimeout = TimeSpan.FromSeconds(20)
        };
      // The timeouts are handled for each request
      return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public HttpWebClient()
      : this(true)
    {
    }

    public HttpWebClient(bool enableCompression)
    {
      EnableCompression = enableCompression;
    }

    #region Properties

    /// <summary>
    /// Gets or sets whether compressed responses are requested and decompressed.
    /// </summary>
    public bool EnableCompression { get; set; }

    /// <summary>
    /// Gets or sets the time in milliseconds to wait for the response of the server (not for the transfer of the content). The default is 20 seconds.
    /// </summary>
    public int RequestTimeout { get; set; } = 20000;

    /// <summary>
    /// Gets or sets the encoding which is used to convert downloaded data to strings.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Gets the headers which are sent with each request.
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    #endregion

    #region Public methods

    public byte[] DownloadData(string url)
    {
      return DownloadDataTaskAsync(url).GetAwaiter().GetResult();
    }

    public byte[] DownloadData(Uri url)
    {
      return DownloadDataTaskAsync(url).GetAwaiter().GetResult();
    }

    public Task<byte[]> DownloadDataTaskAsync(string url)
    {
      return DownloadDataTaskAsync(new Uri(url));
    }

    public Task<byte[]> DownloadDataTaskAsync(Uri url)
    {
      return DownloadDataTaskAsync(url, CancellationToken.None);
    }

    /// <summary>
    /// Downloads the content of the given url. The download can be cancelled with the <paramref name="cancellationToken"/>, this results in
    /// an <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<byte[]> DownloadDataTaskAsync(Uri url, CancellationToken cancellationToken)
    {
      using (HttpResponseMessage response = await SendAsync(url, cancellationToken).ConfigureAwait(false))
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public string DownloadString(string url)
    {
      return DownloadStringTaskAsync(url).GetAwaiter().GetResult();
    }

    public Task<string> DownloadStringTaskAsync(string url)
    {
      return DownloadStringTaskAsync(new Uri(url));
    }

    public async Task<string> DownloadStringTaskAsync(Uri url)
    {
      byte[] data = await DownloadDataTaskAsync(url).ConfigureAwait(false);
      byte[] preamble = Encoding.GetPreamble();
      int start = preamble.Length > 0 && data.Length >= preamble.Length && new ReadOnlySpan<byte>(data, 0, preamble.Length).SequenceEqual(preamble) ? preamble.Length : 0;
      return Encoding.GetString(data, start, data.Length - start);
    }

    public void DownloadFile(string url, string fileName)
    {
      DownloadFile(new Uri(url), fileName);
    }

    public void DownloadFile(Uri url, string fileName)
    {
      using (HttpResponseMessage response = SendAsync(url, CancellationToken.None).GetAwaiter().GetResult())
      using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
        response.Content.CopyToAsync(file).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads the content of the given url and returns it as stream.
    /// </summary>
    public Stream OpenRead(string url)
    {
      return OpenRead(new Uri(url));
    }

    public Stream OpenRead(Uri url)
    {
      return new MemoryStream(DownloadDataTaskAsync(url).GetAwaiter().GetResult(), false);
    }

    public void Dispose()
    {
      // The underlying HttpClient instances are shared
    }

    #endregion

    #region Private methods

    private async Task<HttpResponseMessage> SendAsync(Uri url, CancellationToken cancellationToken)
    {
      using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
      using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
      {
        timeout.CancelAfter(RequestTimeout);
        foreach (KeyValuePair<string, string> header in Headers)
          request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        HttpResponseMessage response;
        try
        {
          response = await (EnableCompression ? COMPRESSED_CLIENT : PLAIN_CLIENT).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
          throw new WebException("The operation has timed out.", ex, WebExceptionStatus.Timeout, null);
        }
        catch (HttpRequestException ex)
        {
          throw new WebException(ex.Message, ex, WebExceptionStatus.ConnectFailure, null);
        }

        if (!response.IsSuccessStatusCode)
        {
          HttpStatusWebException exception = new HttpStatusWebException(response.StatusCode, response.ReasonPhrase);
          response.Dispose();
          throw exception;
        }
        return response;
      }
    }

    #endregion
  }
}
