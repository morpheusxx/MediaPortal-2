﻿#region Copyright (C) 2012-2013 MPExtended
// Copyright (C) 2012-2013 MPExtended Developers, http://www.mpextended.com/
// 
// MPExtended is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MPExtended is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MPExtended. If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MediaPortal.Utilities.SystemAPI;

namespace MediaPortal.Utilities.Process
{
  public class ImpersonationProcess : System.Diagnostics.Process
  {
    private const int HANDLE_FLAG_INHERIT = 1;

    private const uint STARTF_USESHOWWINDOW = 0x00000001;
    private const uint STARTF_USESTDHANDLES = 0x00000100;

    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;

    private const int SW_HIDE = 0;
    private const int SW_MAXIMIZE = 3;
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOW = 5;

    private IntPtr _stdinReadHandle;
    private IntPtr _stdinWriteHandle;
    private IntPtr _stdoutWriteHandle;
    private IntPtr _stderrWriteHandle;
    private IntPtr _stdoutReadHandle;
    private IntPtr _stderrReadHandle;
    private NativeMethods.PROCESS_INFORMATION _processInformation;

    private string GetCommandLine()
    {
      StringBuilder result = new StringBuilder();
      string applicationName = StartInfo.FileName.Trim();
      string arguments = StartInfo.Arguments;

      bool applicationNameIsQuoted = applicationName.StartsWith("\"") && applicationName.EndsWith("\"");

      if (!applicationNameIsQuoted)
        result.Append("\"");

      result.Append(applicationName);

      if (!applicationNameIsQuoted)
        result.Append("\"");

      if (arguments.Length > 0)
      {
        result.Append(" ");
        result.Append(arguments);
      }
      return result.ToString();
    }

    public new void Kill()
    {
      IntPtr hProcess = IntPtr.Zero;
      try
      {
        int id = Id;
        hProcess = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.PROCESS_TERMINATE, false, id);
        if (hProcess == IntPtr.Zero)
          throw new Win32Exception(Marshal.GetLastWin32Error(), "ImpersonationProcess: OpenProcess failed");
        NativeMethods.TerminateProcess(hProcess, 0);
      }
      finally
      {
        ImpersonationHelper.SafeCloseHandle(hProcess);
      }
    }

    public new ProcessPriorityClass PriorityClass
    {
      get { return (ProcessPriorityClass) NativeMethods.GetPriorityClass(_processInformation.hProcess); }
      set { NativeMethods.SetPriorityClass(_processInformation.hProcess, (uint) value); }
    }

    public new int ExitCode
    {
      get
      {
        uint exitCode;
        if (NativeMethods.GetExitCodeProcess(_processInformation.hProcess, out exitCode))
          return (int) exitCode;
        return -1;
      }
    }

    public bool StartAsUser(string domain, string username, string password)
    {
      IntPtr userToken = IntPtr.Zero;
      IntPtr token = IntPtr.Zero;
      try
      {
        if (!NativeMethods.LogonUser(username, domain, password, NativeMethods.LogonType.LOGON32_LOGON_INTERACTIVE, NativeMethods.LogonProvider.LOGON32_PROVIDER_DEFAULT, out token))
          throw new Win32Exception(Marshal.GetLastWin32Error(), String.Format("ImpersonationProcess: LogonUser {0}\\{1} failed", domain, username));

        if (!NativeMethods.DuplicateTokenEx(token, NativeMethods.TOKEN_ASSIGN_PRIMARY | NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY, null, NativeMethods.SecurityImpersonationLevel.SecurityImpersonation, NativeMethods.TOKEN_TYPE.TokenPrimary, out userToken))
          throw new Win32Exception(Marshal.GetLastWin32Error(), "ImpersonationProcess: DuplicateToken failed");

        return StartAsUser(userToken);
      }
      finally
      {
        ImpersonationHelper.SafeCloseHandle(token);
        ImpersonationHelper.SafeCloseHandle(userToken);
      }
    }

    public bool StartAsUser(IntPtr userToken)
    {
      _processInformation = new NativeMethods.PROCESS_INFORMATION();
      NativeMethods.STARTUPINFO startupInfo = new NativeMethods.STARTUPINFO();
      switch (StartInfo.WindowStyle)
      {
        case ProcessWindowStyle.Hidden:
          startupInfo.wShowWindow = SW_HIDE;
          break;
        case ProcessWindowStyle.Maximized:
          startupInfo.wShowWindow = SW_MAXIMIZE;
          break;
        case ProcessWindowStyle.Minimized:
          startupInfo.wShowWindow = SW_MINIMIZE;
          break;
        case ProcessWindowStyle.Normal:
          startupInfo.wShowWindow = SW_SHOW;
          break;
      }
      CreateStandardPipe(out _stdinReadHandle, out _stdinWriteHandle, STD_INPUT_HANDLE, true, StartInfo.RedirectStandardInput);
      CreateStandardPipe(out _stdoutReadHandle, out _stdoutWriteHandle, STD_OUTPUT_HANDLE, false, StartInfo.RedirectStandardOutput);
      CreateStandardPipe(out _stderrReadHandle, out _stderrWriteHandle, STD_ERROR_HANDLE, false, StartInfo.RedirectStandardError);

      startupInfo.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
      startupInfo.hStdInput = _stdinReadHandle;
      startupInfo.hStdOutput = _stdoutWriteHandle;
      startupInfo.hStdError = _stderrWriteHandle;

      NativeMethods.CreateProcessFlags createFlags = NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE | NativeMethods.CreateProcessFlags.CREATE_NEW_PROCESS_GROUP | NativeMethods.CreateProcessFlags.CREATE_DEFAULT_ERROR_MODE;
      if (StartInfo.CreateNoWindow)
      {
        startupInfo.wShowWindow = SW_HIDE;
        createFlags |= NativeMethods.CreateProcessFlags.CREATE_NO_WINDOW;
      }

      // Create process as user, fail hard if this is unsuccessful so it can be caught in EncoderUnit
      if (!NativeMethods.CreateProcessAsUserW(userToken, null, GetCommandLine(), IntPtr.Zero, IntPtr.Zero, true, createFlags, IntPtr.Zero, null, startupInfo, out _processInformation))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "ImpersonationProcess: CreateProcessAsUser failed");

      if (_processInformation.hThread != (IntPtr) (-1))
      {
        NativeMethods.CloseHandle(_processInformation.hThread);
        _processInformation.hThread = IntPtr.Zero;
      }

      if (StartInfo.RedirectStandardInput)
      {
        NativeMethods.CloseHandle(_stdinReadHandle);
        StreamWriter standardInput = new StreamWriter(new FileStream(new SafeFileHandle(_stdinWriteHandle, true), FileAccess.Write, 4096), Console.Out.Encoding) { AutoFlush = true };
        this.SetField("standardInput", standardInput);
      }

      if (StartInfo.RedirectStandardOutput)
      {
        NativeMethods.CloseHandle(_stdoutWriteHandle);
        StreamReader standardOutput = new StreamReader(new FileStream(new SafeFileHandle(_stdoutReadHandle, true), FileAccess.Read, 4096), StartInfo.StandardOutputEncoding);
        this.SetField("standardOutput", standardOutput);
      }

      if (StartInfo.RedirectStandardError)
      {
        NativeMethods.CloseHandle(_stderrWriteHandle);
        StreamReader standardError = new StreamReader(new FileStream(new SafeFileHandle(_stderrReadHandle, true), FileAccess.Read, 4096), StartInfo.StandardErrorEncoding);
        this.SetField("standardError", standardError);
      }

      // Workaround to get process handle as non-public SafeProcessHandle
      Assembly processAssembly = typeof(System.Diagnostics.Process).Assembly;
      Type processManager = processAssembly.GetType("System.Diagnostics.ProcessManager");
      //MethodInfo openProcess = processManager.GetMethod("OpenProcess", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static);
      //object safeProcessHandle = openProcess.Invoke(this, new object[] { _processInformation.dwProcessId, 0x100000, false });
      object safeProcessHandle = processManager.InvokeMember("OpenProcess", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, this, new object[] { _processInformation.dwProcessId, 0x100000, false });

      this.InvokeMethod("SetProcessHandle", safeProcessHandle);
      this.InvokeMethod("SetProcessId", _processInformation.dwProcessId);

      return true;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        ImpersonationHelper.SafeCloseHandle(_processInformation.hProcess);
        ImpersonationHelper.SafeCloseHandle(_processInformation.hThread);
      }
      base.Dispose(disposing);
    }

    private void CreateStandardPipe(out IntPtr readHandle, out IntPtr writeHandle, int standardHandle, bool isInput, bool redirect)
    {
      if (redirect)
      {
        NativeMethods.SECURITY_ATTRIBUTES security = new NativeMethods.SECURITY_ATTRIBUTES { bInheritHandle = true };

        bool success = NativeMethods.CreatePipe(out readHandle, out writeHandle, security, 4096);

        if (success)
          success = NativeMethods.SetHandleInformation(isInput ? writeHandle : readHandle, HANDLE_FLAG_INHERIT, 0);

        if (!success)
          throw new Win32Exception(Marshal.GetLastWin32Error(), "ImpersonationProcess: could not create standard pipe");
      }
      else
      {
        if (isInput)
        {
          writeHandle = IntPtr.Zero;
          readHandle = NativeMethods.GetStdHandle(standardHandle);
        }
        else
        {
          readHandle = IntPtr.Zero;
          writeHandle = NativeMethods.GetStdHandle(standardHandle);
        }
      }
    }
  }

  static class ReflectionExtensions
  {
    public static object InvokeMethod(this object instance, string member, params object[] args)
    {
      return typeof(System.Diagnostics.Process).InvokeMember(member, BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance, null, instance, args);
    }
    public static object SetField(this object instance, string member, params object[] args)
    {
      return typeof(System.Diagnostics.Process).InvokeMember(member, BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance, null, instance, args);
    }
  }
}
