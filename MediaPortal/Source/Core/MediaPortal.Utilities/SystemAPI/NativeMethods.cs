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
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MediaPortal.Utilities.SystemAPI
{
  /// <summary>
  /// This class is a static class containing definitions for system methods that are used in various places in MediaPortal.
  /// </summary>
  public static class NativeMethods
  {
    #region LoadLibraryEx Flags

    public const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
    public const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    #endregion

    /// <summary>
    /// Retrieves a module handle for the specified module. The module must have been loaded by the calling process.
    /// </summary>
    /// <param name="lpModuleName">The name of the loaded module (either a .dll or .exe file).</param>
    /// <returns>If the function succeeds, the return value is a handle to the module.<br></br><br>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandle", CharSet = CharSet.Ansi)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    /// <summary>
    /// The GetProcAddress function retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
    /// </summary>
    /// <param name="hModule">Handle to the DLL module that contains the function or variable. The LoadLibrary or GetModuleHandle function returns this handle.</param>
    /// <param name="lpProcName">Pointer to a null-terminated string containing the function or variable name, or the function's ordinal value. If this parameter is an ordinal value, it must be in the low-order word; the high-order word must be zero.</param>
    /// <returns>If the function succeeds, the return value is the address of the exported function or variable.<br></br><br>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    /// <summary>
    /// Determines whether the specified process is running under WOW64.
    /// </summary>
    /// <param name="hProcess">A handle to the process. The handle must have the PROCESS_QUERY_INFORMATION or PROCESS_QUERY_LIMITED_INFORMATION access right.</param>
    /// <param name="isWow64Process">A pointer to a value that is set to TRUE if the process is running under WOW64. If the process is running under 32-bit Windows, the value is set to FALSE. If the process is a 64-bit application running under 64-bit Windows, the value is also set to FALSE.</param>
    /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
    [DllImport("kernel32.dll", EntryPoint = "IsWow64Process")]
    public static extern bool IsWow64Process(IntPtr hProcess, out bool isWow64Process);

    /// <summary>
    /// The LoadLibrary function maps the specified executable module into the address space of the calling process.
    /// </summary>
    /// <param name="lpLibFileName">Pointer to a null-terminated string that names the executable module (either a .dll or .exe file). The name specified is the file name of the module and is not related to the name stored in the library module itself, as specified by the LIBRARY keyword in the module-definition (.def) file.</param>
    /// <returns>If the function succeeds, the return value is a handle to the module.<br></br><br>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryA", CharSet = CharSet.Ansi)]
    public static extern IntPtr LoadLibrary(string lpLibFileName);

    /// <summary>
    /// Loads the specified module into the address space of the calling process. The specified module may cause other modules to be loaded.
    /// </summary>
    /// <param name="lpFileName">Pointer to a null-terminated string that names the executable module (either a .dll or .exe file). The name specified is the file name of the module and is not related to the name stored in the library module itself, as specified by the LIBRARY keyword in the module-definition (.def) file.</param>
    /// <param name="hFile">This parameter is reserved for future use. It must be IntPtr.Zero.</param>
    /// <param name="dwFlags">The action to be taken when loading the module. If no flags are specified, the behavior of this function is identical to that of the <see cref="LoadLibrary"/> function.</param>
    /// <returns>If the function succeeds, the return value is a handle to the module.<br/>If the function fails, the return value is NULL. To get extended error information, call Marshal.GetLastWin32Error.</returns>
    [DllImport("kernel32.dll")]
    public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    /// <summary>
    /// The FreeLibrary function decrements the reference count of the loaded dynamic-link library (DLL). When the reference count reaches zero, the module is unmapped from the address space of the calling process and the handle is no longer valid.
    /// </summary>
    /// <param name="hLibModule">Handle to the loaded DLL module. The LoadLibrary or GetModuleHandle function returns this handle.</param>
    /// <returns>If the function succeeds, the return value is nonzero.<br></br><br>If the function fails, the return value is zero. To get extended error information, call Marshal.GetLastWin32Error.</br></returns>
    [DllImport("kernel32.dll", EntryPoint = "FreeLibrary", CharSet = CharSet.Ansi)]
    public static extern int FreeLibrary(IntPtr hLibModule);


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
    public static int TOKEN_ASSIGN_PRIMARY = 0x0001;
    public static int TOKEN_DUPLICATE = 0x0002;
    public static int TOKEN_IMPERSONATE = 0x0004;
    public static int TOKEN_QUERY = 0x0008;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
      public IntPtr hProcess;
      public IntPtr hThread;
      public uint dwProcessId;
      public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
      public int cb;
      public string lpReserved;
      public string lpDesktop;
      public string lpTitle;
      public uint dwX;
      public uint dwY;
      public uint dwXSize;
      public uint dwYSize;
      public uint dwXCountChars;
      public uint dwYCountChars;
      public uint dwFillAttribute;
      public uint dwFlags;
      public short wShowWindow;
      public short cbReserved2;
      public IntPtr lpReserved2;
      public IntPtr hStdInput;
      public IntPtr hStdOutput;
      public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SECURITY_ATTRIBUTES
    {
      public uint nLength;
      public IntPtr lpSecurityDescriptor;
      public bool bInheritHandle;
      public SECURITY_ATTRIBUTES()
      {
        nLength = (uint) Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
        lpSecurityDescriptor = IntPtr.Zero;
      }
    }

    public enum TOKEN_TYPE
    {
      TokenPrimary = 1,
      TokenImpersonation
    }

    [Flags]
    public enum CreateProcessFlags : uint
    {
      NONE = 0,
      CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
      CREATE_DEFAULT_ERROR_MODE = 0x04000000,
      CREATE_NEW_CONSOLE = 0x00000010,
      CREATE_NEW_PROCESS_GROUP = 0x00000200,
      CREATE_NO_WINDOW = 0x08000000,
      CREATE_PROTECTED_PROCESS = 0x00040000,
      CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
      CREATE_SEPARATE_WOW_VDM = 0x00000800,
      CREATE_SHARED_WOW_VDM = 0x00001000,
      CREATE_SUSPENDED = 0x00000004,
      CREATE_UNICODE_ENVIRONMENT = 0x00000400,
      DEBUG_ONLY_THIS_PROCESS = 0x00000002,
      DEBUG_PROCESS = 0x00000001,
      DETACHED_PROCESS = 0x00000008,
      EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
      INHERIT_PARENT_AFFINITY = 0x00010000
    }

    [Flags]
    public enum ProcessAccess
    {
      PROCESS_ALL_ACCESS = PROCESS_CREATE_THREAD | PROCESS_DUPLICATE_HANDLE | PROCESS_QUERY_INFORMATION | PROCESS_SET_INFORMATION | PROCESS_TERMINATE | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_SYNCHRONIZE,
      PROCESS_CREATE_THREAD = 0x2,
      PROCESS_DUPLICATE_HANDLE = 0x40,
      PROCESS_QUERY_INFORMATION = 0x400,
      PROCESS_SET_INFORMATION = 0x200,
      PROCESS_TERMINATE = 0x1,
      PROCESS_VM_OPERATION = 0x8,
      PROCESS_VM_READ = 0x10,
      PROCESS_VM_WRITE = 0x20,
      PROCESS_SYNCHRONIZE = 0x100000
    }

    // Obtains user token
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool LogonUser(string username, string domain, string password, LogonType dwLogonType, LogonProvider dwLogonProvider, out IntPtr phToken);

    // Closes open handles returned by LogonUser
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public extern static bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetHandleInformation(IntPtr hObject, int dwMask, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    // Creates duplicate token handle.
    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public extern static bool DuplicateToken(IntPtr existingTokenHandle, SecurityImpersonationLevel impersonationLevel, ref IntPtr duplicateTokenHandle);

    [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx", SetLastError = true)]
    public static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        int dwDesiredAccess,
        SECURITY_ATTRIBUTES lpThreadAttributes,
        SecurityImpersonationLevel impersonationLevel,
        TOKEN_TYPE dwTokenType,
        out IntPtr phNewToken);

    [DllImport("advapi32")]
    public static extern bool OpenProcessToken(
        IntPtr processHandle, // handle to process
        int desiredAccess, // desired access to process
        ref IntPtr tokenHandle // handle to open access token
    );

    [DllImport("advapi32.DLL")]
    public static extern bool ImpersonateLoggedOnUser(IntPtr hToken); // handle to token for logged-on user

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateProcessAsUserW(IntPtr token, [MarshalAs(UnmanagedType.LPTStr)] string lpApplicationName, [MarshalAs(UnmanagedType.LPTStr)] string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, CreateProcessFlags dwCreationFlags, IntPtr lpEnvironment, [MarshalAs(UnmanagedType.LPTStr)] string lpCurrentDirectory, [In] ref NativeMethods.STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);


    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern uint GetPriorityClass(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, SafeFileHandle hSourceHandle, IntPtr hTargetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, bool bInheritHandle, int dwOptions);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

  }
}
