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
using System.Linq;
using System.Security.Principal;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess.LocalFsResourceProvider;
using MediaPortal.Common.Services.Settings;
using MediaPortal.Extensions.ResourceProviders.NetworkNeighborhoodResourceProvider.Impersonate;
using MediaPortal.Extensions.ResourceProviders.NetworkNeighborhoodResourceProvider.Settings;
using MediaPortal.Utilities;
using MediaPortal.Utilities.Exceptions;
using MediaPortal.Utilities.Network;

namespace MediaPortal.Extensions.ResourceProviders.NetworkNeighborhoodResourceProvider
{
  public class NetworkNeighborhoodResourceAccessor : ILocalFsResourceAccessor
  {
    #region Protected fields

    protected NetworkNeighborhoodResourceProvider _parent;
    protected string _path;
    protected ILocalFsResourceAccessor _underlayingResource = null; // Only set if the path points to a file system resource - not a server
    protected WindowsImpersonationContext _windowsImpersonationContext;
    protected WindowsIdentity _impersonatedUser;

    protected static SettingsChangeWatcher<NetworkNeighborhoodResourceProviderSettings> _settings = new SettingsChangeWatcher<NetworkNeighborhoodResourceProviderSettings>();

    #endregion

    public NetworkNeighborhoodResourceAccessor(NetworkNeighborhoodResourceProvider parent, string path)
    {
      _parent = parent;
      _path = path;
      if (IsServerPath(path))
        return;

      _windowsImpersonationContext = ImpersonateUser(ref _impersonatedUser);

      IResourceAccessor ra;
      if (!LocalFsResourceProvider.Instance.TryCreateResourceAccessor("/" + path, out ra))
        throw new IllegalCallException("Unable to access resource '{0}'", path);
      _underlayingResource = (ILocalFsResourceAccessor) ra;
    }

    protected ICollection<IFileSystemResourceAccessor> WrapLocalFsResourceAccessors(ICollection<IFileSystemResourceAccessor> localFsResourceAccessors)
    {
      ICollection<IFileSystemResourceAccessor> result = new List<IFileSystemResourceAccessor>();
      CollectionUtils.AddAll(result, localFsResourceAccessors.Select(resourceAccessor => new NetworkNeighborhoodResourceAccessor(_parent, resourceAccessor.Path.Substring(1))));
      return result;
    }

    protected internal static bool IsServerPath(string providerPath)
    {
      if (!providerPath.StartsWith("//"))
        return false;
      providerPath = StringUtils.RemoveSuffixIfPresent(providerPath.Substring(2), "/"); // Cut leading // and trailing /
      return !providerPath.Contains("/");
    }

    protected internal static bool IsSharePath(string providerPath)
    {
      if (!providerPath.StartsWith("//"))
        return false;
      providerPath = StringUtils.RemoveSuffixIfPresent(providerPath.Substring(2), "/"); // Cut leading // and trailing /
      return providerPath.IndexOf('/') == providerPath.LastIndexOf('/'); // Exactly one /
    }

    protected internal static string GetServerName(string providerPath)
    {
      if (!IsServerPath(providerPath))
        return null;
      return providerPath.Substring(2);
    }

    protected internal static bool IsResource(string path)
    {
      WindowsIdentity current = null;
      using (ImpersonateUser(ref current))
        return IsServerPath(path) || LocalFsResourceProvider.Instance.IsResource("/" + path);
    }

    /// <summary>
    /// Tries to impersonate the current process as the user which runs explorer.exe currently. The caller should always call <see cref="IDisposable.Dispose"/> on
    /// the returned instance to revert identity to self.
    /// </summary>
    private static WindowsImpersonationContext ImpersonateUser(ref WindowsIdentity requestedIdentity)
    {
      NetworkNeighborhoodResourceProviderSettings settings = _settings.Settings;
      WindowsImpersonationContext ctx = null;

      // Prefer to impersonate current interactive user.
      if (settings.ImpersonateInteractive)
      {
        if (!ImpersonationHelper.RequiresImpersonate(requestedIdentity))
          return null;

        ctx = ImpersonationHelper.ImpersonateByProcess("explorer", out requestedIdentity);
      }
      if (ctx != null)
        return ctx;

      // Second way based on network credentials.
      if (settings.UseCredentials)
        ctx = ImpersonationHelper.ImpersonateUser(settings.NetworkUserName, settings.NetworkPassword, out requestedIdentity);

      return ctx;
    }

    #region ILocalFsResourceAccessor implementation

    public void Dispose()
    {
      if (_underlayingResource != null)
        _underlayingResource.Dispose();
      if (_windowsImpersonationContext != null)
        _windowsImpersonationContext.Dispose();
    }

    public IResourceProvider ParentProvider
    {
      get { return _parent; }
    }

    public bool Exists
    {
      get
      {
        using (ImpersonateUser(ref _impersonatedUser))
          return _underlayingResource == null ? IsServerPath(_path) : _underlayingResource.Exists;
      }
    }

    public bool IsFile
    {
      get
      {
        using (ImpersonateUser(ref _impersonatedUser))
          return _underlayingResource != null && _underlayingResource.IsFile;
      }
    }

    public string Path
    {
      get { return _path; }
    }

    public string ResourceName
    {
      get
      {
        using (ImpersonateUser(ref _impersonatedUser))
          return GetServerName(_path) ?? (_underlayingResource == null ? null : _underlayingResource.ResourceName);
      }
    }

    public string ResourcePathName
    {
      get { return LocalFileSystemPath; }
    }

    public ResourcePath CanonicalLocalResourcePath
    {
      get { return ResourcePath.BuildBaseProviderPath(NetworkNeighborhoodResourceProvider.NETWORK_NEIGHBORHOOD_RESOURCE_PROVIDER_ID, _path); }
    }

    public DateTime LastChanged
    {
      get
      {
        using (ImpersonateUser(ref _impersonatedUser))
          return _underlayingResource == null ? new DateTime() : _underlayingResource.LastChanged;
      }
    }

    public long Size
    {
      get
      {
        using (ImpersonateUser(ref _impersonatedUser))
          return _underlayingResource == null ? -1 : _underlayingResource.Size;
      }
    }

    public void PrepareStreamAccess()
    {
      using (ImpersonateUser(ref _impersonatedUser))
        if (_underlayingResource != null)
          _underlayingResource.PrepareStreamAccess();
    }

    public Stream OpenRead()
    {
      if (_underlayingResource == null)
        throw new IllegalCallException("Path '{0} cannot be opened for reading", _path);
      using (ImpersonateUser(ref _impersonatedUser))
        return _underlayingResource.OpenRead();
    }

    public Stream OpenWrite()
    {
      if (_underlayingResource == null)
        throw new IllegalCallException("Path '{0} cannot be opened for reading", _path);
      using (ImpersonateUser(ref _impersonatedUser))
        return _underlayingResource.OpenWrite();
    }

    public IResourceAccessor Clone()
    {
      return new NetworkNeighborhoodResourceAccessor(_parent, _path);
    }

    public bool ResourceExists(string path)
    {
      using (ImpersonateUser(ref _impersonatedUser))
        return IsServerPath(path) || (_underlayingResource != null && _underlayingResource.ResourceExists(path));
    }

    public IFileSystemResourceAccessor GetResource(string path)
    {
      IResourceAccessor ra;
      if (_parent.TryCreateResourceAccessor(ProviderPathHelper.Combine(_path, path), out ra))
        return (IFileSystemResourceAccessor) ra;
      return null;
    }

    public ICollection<IFileSystemResourceAccessor> GetFiles()
    {
      using (ImpersonateUser(ref _impersonatedUser))
      {
        if (_path == "/" || IsServerPath(_path))
          return new List<IFileSystemResourceAccessor>();
        return _underlayingResource == null ? null : WrapLocalFsResourceAccessors(_underlayingResource.GetFiles());
      }
    }

    public ICollection<IFileSystemResourceAccessor> GetChildDirectories()
    {
      using (ImpersonateUser(ref _impersonatedUser))
      {
        if (_path == "/")
          return NetworkResourcesEnumerator.EnumerateResources(ResourceScope.GlobalNet, ResourceType.Disk, ResourceUsage.All, ResourceDisplayType.Server)
            .Select(serverName => new NetworkNeighborhoodResourceAccessor(_parent, StringUtils.CheckPrefix(serverName, @"\\").Replace('\\', '/')))
            .Cast<IFileSystemResourceAccessor>().ToList();
        if (IsServerPath(_path))
          return SharesEnumerator.EnumerateShares(StringUtils.RemovePrefixIfPresent(_path, "//"))
            .Where(share => share.ShareType == ShareType.Disk)
            .Select(
              share =>
              {
                try { return new NetworkNeighborhoodResourceAccessor(_parent, share.UNCPath.Replace('\\', '/')); }
                catch (IllegalCallException) { return null; }
              }
            ).Where(share => share != null).Cast<IFileSystemResourceAccessor>().ToList();
        return _underlayingResource == null ? null : WrapLocalFsResourceAccessors(_underlayingResource.GetChildDirectories());
      }
    }

    public string LocalFileSystemPath
    {
      get { return _path.Replace('/', '\\'); }
    }

    #endregion

    #region Base overrides

    public override string ToString()
    {
      return LocalFileSystemPath;
    }

    #endregion
  }
}
