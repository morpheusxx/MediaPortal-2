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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaPortal.Server.Host
{
  /// <summary>
  /// Runs the <see cref="ServerRuntime"/> inside the generic host: the server is started when the host starts and stopped
  /// when the host shuts down (Ctrl+C, SIGTERM, service stop, IPC shutdown request).
  /// </summary>
  public class ServerHostedService : IHostedService
  {
    private readonly ILogger<ServerHostedService> _logger;
    private readonly ServerRuntime _runtime;
    private bool _started;

    public ServerHostedService(CommandLineOptions options, IHostApplicationLifetime lifetime, ILogger<ServerHostedService> logger)
    {
      _logger = logger;
      _runtime = new ServerRuntime(options.DataDirectory, "ServerHost", () =>
      {
        // Invoked on the IPC thread; stop the host asynchronously with a little delay so that the IPC answer can be sent
        Task.Delay(500).ContinueWith(_ => lifetime.StopApplication());
        return true;
      });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      // Starting the server can take long (database migration, plugin loading), keep the host's startup thread free
      return Task.Run(() =>
      {
        _logger.LogInformation("Starting MP2 server");
        _runtime.Start();
        _started = true;
        _logger.LogInformation("MP2 server started");
      }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      if (!_started)
        return Task.CompletedTask;
      return Task.Run(() =>
      {
        _logger.LogInformation("Stopping MP2 server");
        _runtime.Stop();
        _started = false;
        _logger.LogInformation("MP2 server stopped");
      });
    }
  }
}
