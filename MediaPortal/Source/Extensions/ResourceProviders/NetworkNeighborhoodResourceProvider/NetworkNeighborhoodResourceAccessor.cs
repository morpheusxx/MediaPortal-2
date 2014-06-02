#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess.LocalFsResourceProvider;
using MediaPortal.Common.Services.Settings;
using MediaPortal.Extensions.ResourceProviders.NetworkNeighborhoodResourceProvider.Settings;
using MediaPortal.Utilities;
using MediaPortal.Utilities.Exceptions;
using MediaPortal.Utilities.Network;
using MediaPortal.Utilities.Process;

namespace MediaPortal.Extensions.ResourceProviders.NetworkNeighborhoodResourceProvider
{
  public class NetworkNeighborhoodResourceAccessor : ILocalFsResourceAccessor
  {
    #region Protected fields

    /// <summary>
    /// Contains all opened connections and their reference counts.
    /// </summary>
    protected static IDictionary<NetworkConnection, int> _connections = new Dictionary<NetworkConnection, int>();
    protected static HashSet<string> _connectionFailures = new HashSet<string>();
    protected static object _syncObj = new object();

    protected NetworkNeighborhoodResourceProvider _parent;
    protected string _path;
    protected ILocalFsResourceAccessor _underlayingResource = null; // Only set if the path points to a file system resource - not a server
    protected ImpersonationHelper.ImpersonationContext _impersonationContext;

    protected static SettingsChangeWatcher<NetworkNeighborhoodResourceProviderSettings> _settings = new SettingsChangeWatcher<NetworkNeighborhoodResourceProviderSettings>();

    #endregion

    static NetworkNeighborhoodResourceAccessor()
    {
      _settings.SettingsChanged += SettingsChanged;
    }

    public NetworkNeighborhoodResourceAccessor(NetworkNeighborhoodResourceProvider parent, string path)
    {
      _parent = parent;
      _path = path;
      if (IsServerPath(path))
        return;

      CreateOrUpdateConnection();

      _impersonationContext = ImpersonateUser(null);

      IResourceAccessor ra;
      if (LocalFsResourceProvider.Instance.TryCreateResourceAccessor("/" + path, out ra))
        _underlayingResource = (ILocalFsResourceAccessor)ra;
    }

    #region Connection cache handling

    private static void SettingsChanged(object sender, EventArgs eventArgs)
    {
      // If network settings where changed, invalidate all caches
      ICollection<NetworkConnection> keys;
      lock (_syncObj)
      {
        keys = _connections.Keys;
        _connections.Clear();
        _connectionFailures.Clear();
      }
      foreach (NetworkConnection networkConnection in keys)
        networkConnection.Dispose();
    }

    private void CreateOrUpdateConnection()
    {
      NetworkNeighborhoodResourceProviderSettings settings = _settings.Settings;
      if (!settings.UseCredentials)
        return;

      var shareRoot = ShareRoot;
      lock (_syncObj)
      {
        // Avoid retrying establishing connections, it can take multiple seconds.
        // TODO: check for cache invalidation (i.e. share is not yet ready but will be available later)
        if (_connectionFailures.Contains(shareRoot, StringComparer.OrdinalIgnoreCase))
          return;

        var existingConnection = _connections.Keys.FirstOrDefault(c => c.ToString() == shareRoot);
        if (existingConnection != null)
          _connections[existingConnection]++;
        else
        {
          try
          {
            var newConnection = new NetworkConnection(shareRoot, settings.NetworkUserName, settings.NetworkPassword);
            _connections.Add(newConnection, 0);
          }
          catch (IOException ex)
          {
            _connectionFailures.Add(shareRoot);
            ServiceRegistration.Get<ILogger>().Warn("NetworkNeighborhoodResourceAccessor: Failed to map network drive '{0}'.", ex, shareRoot);
          }
        }
      }
    }

    private string ShareRoot
    {
      get { return NetworkConnection.GetNetworkConnectionRoot(@"\" + LocalFsResourceProviderBase.ToDosPath(_path)); }
    }

    private void CloseConnection()
    {
      string shareRoot = ShareRoot;
      NetworkConnection existingConnection;
      lock (_syncObj)
      {
        existingConnection = _connections.Keys.FirstOrDefault(c => c.ToString() == shareRoot);
        if (existingConnection == null)
          return;

        // Decrement reference counter and dispose connection once it gets 0.
        _connections[existingConnection]--;
        if (_connections[existingConnection] != 0)
          return;
        _connections.Remove(existingConnection);
        ServiceRegistration.Get<ILogger>().Debug("NetworkNeighborhoodResourceAccessor: Closing network drive '{0}'.", existingConnection);
      }
      existingConnection.Dispose();
    }

    #endregion

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
      using (ImpersonateUser(null))
        return IsServerPath(path) || LocalFsResourceProvider.Instance.IsResource("/" + path);
    }

    /// <summary>
    /// Tries to impersonate the current process as the user which runs explorer.exe currently. The caller should always call <see cref="IDisposable.Dispose"/> on
    /// the returned instance to revert identity to self.
    /// </summary>
    private static ImpersonationHelper.ImpersonationContext ImpersonateUser(ImpersonationHelper.ImpersonationContext requestedIdentity)
    {
      NetworkNeighborhoodResourceProviderSettings settings = _settings.Settings;
      ImpersonationHelper.ImpersonationContext ctx = null;

      // Prefer to impersonate current interactive user.
      if (settings.ImpersonateInteractive)
      {
        if (requestedIdentity != null && !ImpersonationHelper.RequiresImpersonate(requestedIdentity.Identity))
          return null;

        ctx = ImpersonationHelper.ImpersonateByProcess("explorer");
      }
      return ctx;
    }

    #region ILocalFsResourceAccessor implementation

    public void Dispose()
    {
      if (_underlayingResource != null)
        _underlayingResource.Dispose();
      if (_impersonationContext != null)
        _impersonationContext.Dispose();
      CloseConnection();
    }

    public IResourceProvider ParentProvider
    {
      get { return _parent; }
    }

    public bool Exists
    {
      get
      {
        using (ImpersonateUser(_impersonationContext))
          return _underlayingResource == null ? IsServerPath(_path) : _underlayingResource.Exists;
      }
    }

    public bool IsFile
    {
      get
      {
        using (ImpersonateUser(_impersonationContext))
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
        using (ImpersonateUser(_impersonationContext))
          return GetServerName(_path) ?? (_underlayingResource == null ? string.Empty : _underlayingResource.ResourceName);
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
        using (ImpersonateUser(_impersonationContext))
          return _underlayingResource == null ? new DateTime() : _underlayingResource.LastChanged;
      }
    }

    public long Size
    {
      get
      {
        using (ImpersonateUser(_impersonationContext))
          return _underlayingResource == null ? -1 : _underlayingResource.Size;
      }
    }

    public void PrepareStreamAccess()
    {
      using (ImpersonateUser(_impersonationContext))
        if (_underlayingResource != null)
          _underlayingResource.PrepareStreamAccess();
    }

    public Stream OpenRead()
    {
      if (_underlayingResource == null)
        return null;
      using (ImpersonateUser(_impersonationContext))
        return _underlayingResource.OpenRead();
    }

    public Stream OpenWrite()
    {
      if (_underlayingResource == null)
        return null;
      using (ImpersonateUser(_impersonationContext))
        return _underlayingResource.OpenWrite();
    }

    public IResourceAccessor Clone()
    {
      return new NetworkNeighborhoodResourceAccessor(_parent, _path);
    }

    public bool ResourceExists(string path)
    {
      using (ImpersonateUser(_impersonationContext))
        return IsServerPath(path) || (_underlayingResource != null && _underlayingResource.ResourceExists(path));
    }

    public IFileSystemResourceAccessor GetResource(string path)
    {
      IResourceAccessor ra;
      if (_parent.TryCreateResourceAccessor(ProviderPathHelper.Combine(_path, path), out ra))
        return (IFileSystemResourceAccessor)ra;
      return null;
    }

    public ICollection<IFileSystemResourceAccessor> GetFiles()
    {
      using (ImpersonateUser(_impersonationContext))
      {
        if (_path == "/" || IsServerPath(_path))
          return new List<IFileSystemResourceAccessor>();
        return _underlayingResource == null ? null : WrapLocalFsResourceAccessors(_underlayingResource.GetFiles());
      }
    }

    public ICollection<IFileSystemResourceAccessor> GetChildDirectories()
    {
      using (ImpersonateUser(_impersonationContext))
      {
        if (_path == "/")
          return NetworkResourcesEnumerator.EnumerateResources(ResourceScope.GlobalNet, ResourceType.Disk, ResourceUsage.All, ResourceDisplayType.Server)
            .Select(serverName => new NetworkNeighborhoodResourceAccessor(_parent, StringUtils.CheckPrefix(serverName, @"\\").Replace('\\', '/')))
            .Cast<IFileSystemResourceAccessor>().ToList();
        if (IsServerPath(_path))
          return SharesEnumerator.EnumerateShares(StringUtils.RemovePrefixIfPresent(_path, "//"))
            // Allow all filesystems, but exclude "Special" shares (IPC, Admin$)
            .Where(share => share.IsFileSystem && !share.ShareType.HasFlag(ShareType.Special))
            .Select(
              share =>
              {
                try { return new NetworkNeighborhoodResourceAccessor(_parent, share.UNCPath.Replace('\\', '/')); }
                catch (IllegalCallException) { return null; }
              }
            ).Where(share => share != null && share.Exists).Cast<IFileSystemResourceAccessor>().ToList(); // "share.Exists" considers the user's access rights.
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
