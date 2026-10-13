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
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Utilities.Process;

namespace MediaPortal.Common.Services.ResourceAccess.ImpersonationService
{
  /// <summary>
  /// <see cref="IImpersonationService"/> for platforms where credential based impersonation is not available
  /// (everything but Windows). Credentials cannot be registered; all actions and processes run as the current user.
  /// </summary>
  internal sealed class NoImpersonationService : IImpersonationService
  {
    public bool TryRegisterCredential(ResourcePath path, NetworkCredential credential)
    {
      ServiceRegistration.Get<ILogger>().Warn("ImpersonationService: Registering credentials is not supported on this platform (ResourcePath '{0}')", path);
      return false;
    }

    public bool TryUnregisterCredential(ResourcePath path)
    {
      return false;
    }

    public void RunImpersonatedFor(ResourcePath path, Action action)
    {
      action.Invoke();
    }

    public T RunImpersonatedFor<T>(ResourcePath path, Func<T> func)
    {
      return func.Invoke();
    }

    public Task<ProcessExecutionResult> ExecuteWithResourceAccessAsync(ResourcePath path, string executable, string arguments, ProcessPriorityClass priorityClass, int maxWaitMs)
    {
      return ProcessUtils.ExecuteAsync(executable, arguments, startInfo => CreateProcessWithResourceAccess(path, startInfo), priorityClass, maxWaitMs);
    }

    public IProcess CreateProcessWithResourceAccess(ResourcePath path, ProcessStartInfo startInfo)
    {
      return ProcessUtils.Create(startInfo);
    }

    public void Dispose() { }
  }
}
