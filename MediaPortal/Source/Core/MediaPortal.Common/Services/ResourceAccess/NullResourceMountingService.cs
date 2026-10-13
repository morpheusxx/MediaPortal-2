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
using System.Collections.Generic;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;

namespace MediaPortal.Common.Services.ResourceAccess
{
  /// <summary>
  /// <see cref="IResourceMountingService"/> for platforms without a virtual filesystem driver (everything but Windows/Dokan).
  /// Mounting is reported as unavailable; callers already handle this (<see cref="CreateRootDirectory"/> returns <c>null</c>).
  /// </summary>
  public class NullResourceMountingService : IResourceMountingService
  {
    public ResourcePath RootPath
    {
      get { return null; }
    }

    public ICollection<string> RootDirectories
    {
      get { return new List<string>(); }
    }

    public void Startup()
    {
      ServiceRegistration.Get<ILogger>().Info("NullResourceMountingService: Mounting remote resources into the local filesystem is not supported on this platform");
    }

    public void Shutdown() { }

    public bool IsVirtualResource(ResourcePath rp)
    {
      return false;
    }

    public string CreateRootDirectory(string rootDirectoryName)
    {
      return null;
    }

    public void DisposeRootDirectory(string rootDirectoryName) { }

    public ICollection<IFileSystemResourceAccessor> GetResources(string rootDirectoryName)
    {
      return new List<IFileSystemResourceAccessor>();
    }

    public string AddResource(string rootDirectoryName, IFileSystemResourceAccessor resourceAccessor)
    {
      return null;
    }

    public void RemoveResource(string rootDirectoryName, IFileSystemResourceAccessor resourceAccessor) { }
  }
}
