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
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaPortal.Server.Host
{
  /// <summary>
  /// Platform independent host of the MP2 server. Runs as console application (Ctrl+C / SIGTERM stops it), as systemd service
  /// (<c>Type=notify</c>), as Windows service and inside containers. For the Windows UI/tray variant see <c>MP2-Server.exe</c>.
  /// </summary>
  public static class Program
  {
    public static async Task<int> Main(string[] args)
    {
      CommandLineOptions options = null;
      int exitCode = 0;
      Parser.Default.ParseArguments<CommandLineOptions>(args)
        .WithParsed(parsed => options = parsed)
        .WithNotParsed(err => exitCode = 1);
      if (options == null)
        return exitCode;

      // MP2 has its own logging; the host's logging is only used for lifetime messages (and systemd/journal integration)
      HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = Array.Empty<string>() });
      builder.Logging.SetMinimumLevel(LogLevel.Information);
      builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromMinutes(2));
      builder.Services.AddSystemd();
      builder.Services.AddWindowsService(o => o.ServiceName = "MP2 Server Host");
      builder.Services.AddSingleton(options);
      builder.Services.AddHostedService<ServerHostedService>();

      using (IHost host = builder.Build())
      {
        await host.RunAsync();
      }
      return 0;
    }
  }
}
