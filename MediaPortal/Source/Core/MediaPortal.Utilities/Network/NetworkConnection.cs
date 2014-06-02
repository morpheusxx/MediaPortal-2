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
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace MediaPortal.Utilities.Network
{
  /// <summary>
  /// Wrapper class for a network connection to a given UNC path, using username and password.
  /// </summary>
  public class NetworkConnection : IDisposable
  {
    #region Fields

    readonly string _networkName;

    #endregion

    #region Windows API functions

    /// <summary>
    /// The WNetAddConnection2 function makes a connection to a network resource and can redirect a local device to the network resource.
    /// </summary>
    /// <param name="netResource">A pointer to a <see cref="NetResource"/> structure that specifies details of the proposed connection, such as information about the network resource, the local device, and the network resource provider.</param>
    /// <param name="password">A pointer to a constant null-terminated string that specifies a password to be used in making the network connection. If lpPassword is NULL, the function uses the current default password associated with the user specified by the lpUserName parameter. If lpPassword points to an empty string, the function does not use a password. If the connection fails because of an invalid password and the CONNECT_INTERACTIVE value is set in the dwFlags parameter, the function displays a dialog box asking the user to type the password.</param>
    /// <param name="username">A pointer to a constant null-terminated string that specifies a user name for making the connection. If lpUserName is NULL, the function uses the default user name. (The user context for the process provides the default user name.) The lpUserName parameter is specified when users want to connect to a network resource for which they have been assigned a user name or account other than the default user name or account. The user-name string represents a security context. It may be specific to a network provider.</param>
    /// <param name="flags">A set of connection options. The possible values for the connection options are defined in the Winnetwk.h header file. The following values can currently be used.</param>
    /// <returns>If the function succeeds, the return value is <c>0</c> (NO_ERROR).</returns>
    [DllImport("mpr.dll")]
    protected static extern int WNetAddConnection2(NetResource netResource, string password, string username, int flags);

    /// <summary>
    /// The WNetCancelConnection2 function cancels an existing network connection. You can also call the function to remove remembered network connections that are not currently connected.
    /// </summary>
    /// <param name="name">Pointer to a constant null-terminated string that specifies the name of either the redirected local device or the remote network resource to disconnect from. If this parameter specifies a redirected local device, the function cancels only the specified device redirection. If the parameter specifies a remote network resource, all connections without devices are canceled.</param>
    /// <param name="flags">Connection type. The following values are defined. </param>
    /// <param name="force">Specifies whether the disconnection should occur if there are open files or jobs on the connection. If this parameter is <c>false</c>, the function fails if there are open files or jobs.</param>
    /// <returns>If the function succeeds, the return value is <c>0</c> (NO_ERROR).</returns>
    [DllImport("mpr.dll")]
    protected static extern int WNetCancelConnection2(string name, int flags, bool force);

    #endregion

    /// <summary>
    /// Gets the "root" of the connection. This part consists only of \\SERVER\SHARE, even if the connection was established to a deeper path
    /// </summary>
    public static string GetNetworkConnectionRoot(string networkPath)
    {
      return string.Join("\\", networkPath.Split('\\').Take(4).ToArray());
    }

    /// <summary>
    /// Constructs a new <see cref="NetworkConnection"/>.
    /// </summary>
    /// <param name="networkName">UNC path to connect</param>
    /// <param name="userName">Username</param>
    /// <param name="password">Password</param>
    /// <exception cref="IOException"/>
    public NetworkConnection(string networkName, string userName, string password)
      : this(networkName, new NetworkCredential(userName, password))
    {
    }

    /// <summary>
    /// Constructs a new <see cref="NetworkConnection"/>.
    /// </summary>
    /// <param name="networkName">UNC path to connect</param>
    /// <param name="credentials">Credentials</param>
    /// <exception cref="IOException"/>
    public NetworkConnection(string networkName, NetworkCredential credentials)
    {
      _networkName = networkName;

      var netResource = new NetResource
      {
        Scope = ResourceScope.GlobalNet,
        ResourceType = ResourceType.Disk,
        DisplayType = ResourceDisplayType.Share,
        RemoteName = _networkName
      };

      var result = WNetAddConnection2(
          netResource,
          credentials.Password,
          credentials.UserName,
          0);

      if (result != 0)
        throw new IOException("Error connecting to remote share", result);
    }

    ~NetworkConnection()
    {
      Dispose(false);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      WNetCancelConnection2(_networkName, 0, true);
    }

    public override int GetHashCode()
    {
      return (_networkName != null ? _networkName.GetHashCode() : 0);
    }

    protected bool Equals(NetworkConnection other)
    {
      return string.Equals(_networkName, other._networkName);
    }

    public override bool Equals(object obj)
    {
      NetworkConnection other = obj as NetworkConnection;
      return other != null && string.Equals(_networkName, other._networkName, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
      return _networkName;
    }
  }
}
