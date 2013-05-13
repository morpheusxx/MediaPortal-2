﻿#region Copyright (C) 2007-2013 Team MediaPortal

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
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace MediaPortal.Utilities.Process
{
  /// <summary>
  /// Helper class to logon as a new user. This is be required to access network resources when running the main program as LocalSystem.
  /// </summary>
  public class ImpersonationHelper
  {
    #region ImpersonationContext

    /// <summary>
    /// Helper class to store <see cref="Identity"/> and automatically impersonate.
    /// </summary>
    public class ImpersonationContext : IDisposable
    {
      private WindowsIdentity _identity;

      public WindowsIdentity Identity
      {
        get { return _identity; }
        set
        {
          _identity = value;
          try
          {
            Context = value.Impersonate();
          }
          catch { }
        }
      }

      public WindowsImpersonationContext Context { get; private set; }

      public void Dispose()
      {
        WindowsImpersonationContext wic = Context;
        if (wic != null)
          wic.Dispose();
      }
    }

    #endregion

    #region Constants and imports

    private static readonly WellKnownSidType[] KNOWN_SID_TYPES = new[] { WellKnownSidType.NetworkServiceSid, WellKnownSidType.LocalServiceSid, WellKnownSidType.LocalSystemSid };

    // Group type enum
    public enum SecurityImpersonationLevel
    {
      SecurityAnonymous = 0,
      SecurityIdentification = 1,
      SecurityImpersonation = 2,
      SecurityDelegation = 3
    }

    public enum LogonType
    {
      /// <summary>
      /// This logon type is intended for users who will be interactively using the computer, such as a user being logged on  
      /// by a terminal server, remote shell, or similar process.
      /// This logon type has the additional expense of caching logon information for disconnected operations;
      /// therefore, it is inappropriate for some client/server applications,
      /// such as a mail server.
      /// </summary>
      LOGON32_LOGON_INTERACTIVE = 2,

      /// <summary>
      /// This logon type is intended for high performance servers to authenticate plaintext passwords.

      /// The LogonUser function does not cache credentials for this logon type.
      /// </summary>
      LOGON32_LOGON_NETWORK = 3,

      /// <summary>
      /// This logon type is intended for batch servers, where processes may be executing on behalf of a user without
      /// their direct intervention. This type is also for higher performance servers that process many plaintext
      /// authentication attempts at a time, such as mail or Web servers.
      /// The LogonUser function does not cache credentials for this logon type.
      /// </summary>
      LOGON32_LOGON_BATCH = 4,

      /// <summary>
      /// Indicates a service-type logon. The account provided must have the service privilege enabled.
      /// </summary>
      LOGON32_LOGON_SERVICE = 5,

      /// <summary>
      /// This logon type is for GINA DLLs that log on users who will be interactively using the computer.
      /// This logon type can generate a unique audit record that shows when the workstation was unlocked.
      /// </summary>
      LOGON32_LOGON_UNLOCK = 7,

      /// <summary>
      /// This logon type preserves the name and password in the authentication package, which allows the server to make
      /// connections to other network servers while impersonating the client. A server can accept plaintext credentials
      /// from a client, call LogonUser, verify that the user can access the system across the network, and still
      /// communicate with other servers.
      /// NOTE: Windows NT:  This value is not supported.
      /// </summary>
      LOGON32_LOGON_NETWORK_CLEARTEXT = 8,

      /// <summary>
      /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
      /// The new logon session has the same local identifier but uses different credentials for other network connections.
      /// NOTE: This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
      /// NOTE: Windows NT:  This value is not supported.
      /// </summary>
      LOGON32_LOGON_NEW_CREDENTIALS = 9,
    }

    public enum LogonProvider
    {
      /// <summary>
      /// Use the standard logon provider for the system.
      /// The default security provider is negotiate, unless you pass NULL for the domain name and the user name
      /// is not in UPN format. In this case, the default provider is NTLM.
      /// NOTE: Windows 2000/NT:   The default security provider is NTLM.
      /// </summary>
      LOGON32_PROVIDER_DEFAULT = 0,
      LOGON32_PROVIDER_WINNT35 = 1,
      LOGON32_PROVIDER_WINNT40 = 2,
      LOGON32_PROVIDER_WINNT50 = 3
    }

    private static int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
    private static int STANDARD_RIGHTS_READ = 0x00020000;
    private static int TOKEN_ASSIGN_PRIMARY = 0x0001;
    private static int TOKEN_DUPLICATE = 0x0002;
    private static int TOKEN_IMPERSONATE = 0x0004;
    private static int TOKEN_QUERY = 0x0008;
    private static int TOKEN_QUERY_SOURCE = 0x0010;
    private static int TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private static int TOKEN_ADJUST_GROUPS = 0x0040;
    private static int TOKEN_ADJUST_DEFAULT = 0x0080;
    private static int TOKEN_ADJUST_SESSIONID = 0x0100;
    private static int TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
    private static int TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
        TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
        TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
        TOKEN_ADJUST_SESSIONID);

    // Obtains user token
    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool LogonUser(string pszUsername, string pszDomain, string pszPassword, LogonType dwLogonType, LogonProvider dwLogonProvider, ref IntPtr phToken);

    // Closes open handles returned by LogonUser
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    internal extern static bool CloseHandle(IntPtr handle);

    // Creates duplicate token handle.
    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal extern static bool DuplicateToken(IntPtr existingTokenHandle, SecurityImpersonationLevel impersonationLevel, ref IntPtr duplicateTokenHandle);

    [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        int dwDesiredAccess,
        ProcessUtils.SECURITY_ATTRIBUTES lpThreadAttributes,
        SecurityImpersonationLevel impersonationLevel,
        ProcessUtils.TOKEN_TYPE dwTokenType,
        ref IntPtr phNewToken);

    [DllImport("advapi32")]
    internal static extern bool OpenProcessToken(
        IntPtr processHandle, // handle to process
        int desiredAccess, // desired access to process
        ref IntPtr tokenHandle // handle to open access token
    );

    [DllImport("advapi32.DLL")]
    public static extern bool ImpersonateLoggedOnUser(IntPtr hToken); // handle to token for logged-on user

    #endregion

    /// <summary>
    /// Checks if the caller needs to impersonate (again).
    /// </summary>
    /// <returns><c>true</c> if impersonate is required.</returns>
    public static bool RequiresImpersonate(WindowsIdentity requestedIdentity)
    {
      if (requestedIdentity == null)
        return true;

      WindowsIdentity current = WindowsIdentity.GetCurrent();
      if (current == null || current.User == null) // Can never happen, just to avoid R# warning.
        return true;

      return
        current.User != requestedIdentity.User || /* Current user is not the requested one. We need to compare SIDs here, instances are not equal */
        IsWellknownIdentity(current) /* User is any of well known SIDs, those have no network access */;
    }

    /// <summary>
    /// Indicates if the <see cref="WindowsIdentity.GetCurrent()"/> represents one of the <see cref="KNOWN_SID_TYPES"/>, which do not have network access.
    /// </summary>
    /// <returns><c>true</c> for a well known identity.</returns>
    public static bool IsWellknownIdentity()
    {
      return IsWellknownIdentity(WindowsIdentity.GetCurrent());
    }

    /// <summary>
    /// Indicates if the given <paramref name="identity"/> represents one of the <see cref="KNOWN_SID_TYPES"/>, which do not have network access.
    /// </summary>
    /// <param name="identity">Identity to check.</param>
    /// <returns><c>true</c> for a well known identity.</returns>
    public static bool IsWellknownIdentity(WindowsIdentity identity)
    {
      return KNOWN_SID_TYPES.Any(wellKnownSidType => identity.User != null && identity.User.IsWellKnown(wellKnownSidType));
    }

    /// <summary>
    /// Attempts to impersonate an user based on an running process. If successful, it returns a WindowsImpersonationContext of the new users identity.
    /// </summary>
    /// <param name="processName">Process name to take user account from (without .exe).</param>
    /// <returns>WindowsImpersonationContext if successful.</returns>
    public static ImpersonationContext ImpersonateByProcess(string processName)
    {
      IntPtr userToken;
      if (!GetTokenByProcess(processName, out userToken))
        return null;

      try
      {
        return new ImpersonationContext { Identity = new WindowsIdentity(userToken) };
      }
      finally
      {
        // Close handle.
        SafeCloseHandle(userToken);
      }
    }

    /// <summary>
    /// Attempts to impersonate an user. If successful, it returns a WindowsImpersonationContext of the new users identity.
    /// </summary>
    /// <param name="username">Username you want to impersonate.</param>
    /// <param name="password">User's password to logon with.</param>
    /// <param name="domain">Logon domain, defaults to local system.</param>
    /// <returns>WindowsImpersonationContext if successful.</returns>
    public static ImpersonationContext ImpersonateUser(string username, string password, string domain = null)
    {
      // Initialize tokens
      IntPtr userToken = IntPtr.Zero;

      try
      {
        if (!GetTokenByUser(username, password, domain, out userToken))
          return null;

        // Create new identity using new primary token.
        return new ImpersonationContext { Identity = new WindowsIdentity(userToken) };
      }
      finally
      {
        // Close handle(s)
        SafeCloseHandle(userToken);
      }
    }

    /// <summary>
    /// Tries to get an existing user token running <c>explorer.exe</c>. If <paramref name="duplicate"/> is set to <c>true</c>, the caller must call <see cref="CloseHandle"/> 
    /// for the returned <paramref name="existingTokenHandle"/>  when it is no longer required.
    /// </summary>
    /// <param name="existingTokenHandle">Outputs an existing token.</param>
    /// <param name="duplicate"><c>true</c> to duplicate handle.</param>
    public static bool GetTokenByProcess(out IntPtr existingTokenHandle, bool duplicate = false)
    {
      return GetTokenByProcess("explorer", out existingTokenHandle, duplicate);
    }

    /// <summary>
    /// Tries to get an existing user token from the given <paramref name="processName"/>. If <paramref name="duplicate"/> is set to <c>true</c>, the caller must call <see cref="CloseHandle"/> 
    /// for the returned <paramref name="existingTokenHandle"/>  when it is no longer required.
    /// </summary>
    /// <param name="processName">Process name to take user account from (without .exe).</param>
    /// <param name="existingTokenHandle">Outputs an existing token.</param>
    /// <param name="duplicate"><c>true</c> to duplicate handle.</param>
    /// <returns><c>true</c> if successful.</returns>
    public static bool GetTokenByProcess(string processName, out IntPtr existingTokenHandle, bool duplicate = false)
    {
      // Try to find a process for given processName. There can be multiple processes, we will take the first one.
      // Attention: when working on a RemoteDesktop/Terminal session, there can be multiple user logged in. The result of finding the first process
      // might be not deterministic.
      existingTokenHandle = IntPtr.Zero;
      System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault();
      if (process == null)
        return false;

      try
      {
        if (!OpenProcessToken(process.Handle, TOKEN_QUERY | TOKEN_IMPERSONATE | TOKEN_DUPLICATE, ref existingTokenHandle))
          return false;

        IntPtr impersonationToken = existingTokenHandle;
        return !duplicate || CreatePrimaryToken(impersonationToken, out existingTokenHandle);
      }
      catch
      { }
      return false;
    }

    private static bool CreatePrimaryToken(IntPtr impersonationToken, out IntPtr primaryToken)
    {
      primaryToken = impersonationToken;

      // Convert the impersonation token into Primary token
      ProcessUtils.SECURITY_ATTRIBUTES sa = new ProcessUtils.SECURITY_ATTRIBUTES();
      sa.nLength = (uint) Marshal.SizeOf(sa);
      
      bool retVal = DuplicateTokenEx(
        impersonationToken,
        TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY,
        sa,
        SecurityImpersonationLevel.SecurityIdentification,
        ProcessUtils.TOKEN_TYPE.TokenPrimary,
        ref primaryToken);

      // Close the Token that was previously opened.
      CloseHandle(impersonationToken);
      return retVal;
    }

    /// <summary>
    /// Tries to create a new user token based on given user credentials. Caller must call <see cref="CloseHandle"/> for the returned <paramref name="duplicateTokenHandle"/>
    /// when it is no longer required.
    /// </summary>
    /// <param name="uername">User name.</param>
    /// <param name="password">Password.</param>
    /// <param name="domain">Domain name, <c>null</c> defaults to computer name.</param>
    /// <param name="duplicateTokenHandle">Outputs a duplicated token.</param>
    /// <returns><c>true</c> if successful.</returns>
    public static bool GetTokenByUser(string uername, string password, string domain, out IntPtr duplicateTokenHandle)
    {
      // Initialize tokens
      duplicateTokenHandle = IntPtr.Zero;
      IntPtr existingTokenHandle = IntPtr.Zero;

      // If domain name was blank, assume local machine
      if (string.IsNullOrWhiteSpace(domain))
        domain = Environment.MachineName;

      try
      {
        // Get handle to token
        if (!LogonUser(uername, domain, password, LogonType.LOGON32_LOGON_INTERACTIVE, LogonProvider.LOGON32_PROVIDER_DEFAULT, ref existingTokenHandle))
          return false;

        return DuplicateToken(existingTokenHandle, SecurityImpersonationLevel.SecurityImpersonation, ref duplicateTokenHandle);
      }
      finally
      {
        // Close handle(s)
        SafeCloseHandle(existingTokenHandle);
      }
    }

    internal static void SafeCloseHandle(IntPtr handle)
    {
      if (handle != IntPtr.Zero)
        CloseHandle(handle);
    }
  }
}
