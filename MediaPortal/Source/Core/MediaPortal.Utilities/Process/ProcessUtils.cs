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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace MediaPortal.Utilities.Process
{
  public class ProcessUtils
  {
    private static readonly Encoding CONSOLE_ENCODING = Encoding.UTF8;
    private static readonly string CONSOLE_ENCODING_PREAMBLE = CONSOLE_ENCODING.GetString(CONSOLE_ENCODING.GetPreamble());

    #region Imports and consts

    // ReSharper disable InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
      public IntPtr hProcess;
      public IntPtr hThread;
      public uint dwProcessId;
      public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class SECURITY_ATTRIBUTES
    {
      public uint nLength;
      public IntPtr lpSecurityDescriptor;
      public bool bInheritHandle;
      public SECURITY_ATTRIBUTES()
      {
        nLength = 12;
        lpSecurityDescriptor = IntPtr.Zero;
      }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class STARTUPINFO
    {
      public uint cb;
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
      public SafeFileHandle hStdInput;
      public SafeFileHandle hStdOutput;
      public SafeFileHandle hStdError;

      public STARTUPINFO()
      {
        cb = (uint) Marshal.SizeOf(this);
        hStdInput = new SafeFileHandle(IntPtr.Zero, false);
        hStdOutput = new SafeFileHandle(IntPtr.Zero, false);
        hStdError = new SafeFileHandle(IntPtr.Zero, false);
      }
    }

    internal enum SECURITY_IMPERSONATION_LEVEL
    {
      SecurityAnonymous,
      SecurityIdentification,
      SecurityImpersonation,
      SecurityDelegation
    }

    internal enum TOKEN_TYPE
    {
      TokenPrimary = 1,
      TokenImpersonation
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string lpApplicationName,
        string lpCommandLine,
        SECURITY_ATTRIBUTES lpProcessAttributes,
        SECURITY_ATTRIBUTES lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, SafeFileHandle hSourceHandle, IntPtr hTargetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, bool bInheritHandle, int dwOptions);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern IntPtr GetCurrentProcess();

    private const int INFINITE = -1;

    private const short SW_HIDE = 0;
    private const short SW_SHOW = 5;

    private const uint STARTF_USESTDHANDLES = 0x00000100;

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;

    // ReSharper restore InconsistentNaming

    #endregion

    #region Internal classes

    class ProcessWaitHandle : WaitHandle
    {
      public ProcessWaitHandle(IntPtr processHandle)
      {
        SafeWaitHandle = new SafeWaitHandle(processHandle, false);
      }
    }

    #endregion

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion. If the process doesn't end in 
    /// this time, it gets aborted.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns><c>true</c> if process was executed and finished correctly</returns>
    public static bool TryExecute(string executable, string arguments, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = INFINITE)
    {
      string unused;
      return TryExecute(executable, arguments, false, out unused, priorityClass, maxWaitMs);
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion. If the process doesn't end in 
    /// this time, it gets aborted. This helper method automatically decides if an impersonation should be done, depending on the current identity's 
    /// <see cref="TokenImpersonationLevel"/>.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns><c>true</c> if process was executed and finished correctly</returns>
    public static bool TryExecute_AutoImpersonate(string executable, string arguments, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = INFINITE)
    {
      return IsImpersonated ?
        TryExecute_Impersonated(executable, arguments, priorityClass, maxWaitMs) :
        TryExecute(executable, arguments, priorityClass, maxWaitMs);
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion. If the process doesn't end in 
    /// this time, it gets aborted. This method tries to impersonate the interactive user and run the process under its identity.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns><c>true</c> if process was executed and finished correctly</returns>
    public static bool TryExecute_Impersonated(string executable, string arguments, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = INFINITE)
    {
      IntPtr userToken;
      if (!ImpersonationHelper.GetTokenByProcess(out userToken, true))
        return false;

      string unused;
      return TryExecute_Impersonated(executable, arguments, userToken, false, out unused, priorityClass, maxWaitMs);
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion and returns the contents of
    /// <see cref="Process.StandardOutput"/>. If the process doesn't end in this time, it gets aborted.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="result">Returns the contents of standard output</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns></returns>
    public static bool TryExecuteReadString(string executable, string arguments, out string result, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = 1000)
    {
      return TryExecute(executable, arguments, true, out result, priorityClass, maxWaitMs);
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion and returns the contents of
    /// <see cref="Process.StandardOutput"/>. If the process doesn't end in this time, it gets aborted.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="result">Returns the contents of standard output</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns></returns>
    public static bool TryExecuteReadString_AutoImpersonate(string executable, string arguments, out string result, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = 1000)
    {
      return IsImpersonated ?
        TryExecuteReadString_Impersonated(executable, arguments, out result, priorityClass, maxWaitMs) :
        TryExecuteReadString(executable, arguments, out result, priorityClass, maxWaitMs);
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion and returns the contents of
    /// <see cref="Process.StandardOutput"/>. If the process doesn't end in this time, it gets aborted. 
    /// This method tries to impersonate the interactive user and run the process under its identity.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="result">Returns the contents of standard output</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns><c>true</c> if process was executed and finished correctly</returns>
    public static bool TryExecuteReadString_Impersonated(string executable, string arguments, out string result, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = INFINITE)
    {
      IntPtr userToken;
      if (!ImpersonationHelper.GetTokenByProcess(out userToken, true))
      {
        result = null;
        return false;
      }
      return TryExecute_Impersonated(executable, arguments, userToken, true, out result, priorityClass, maxWaitMs);
    }

    #region Private methods

    /// <summary>
    /// Indicates if the current <see cref="WindowsIdentity"/> uses impersonation.
    /// </summary>
    private static bool IsImpersonated
    {
      get
      {
        WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
        return windowsIdentity != null && windowsIdentity.ImpersonationLevel == TokenImpersonationLevel.Impersonation;
      }
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion and returns the contents of
    /// <see cref="Process.StandardOutput"/>. If the process doesn't end in this time, it gets aborted.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="redirectInputOutput"><c>true</c> to redirect standard streams.</param>
    /// <param name="result">Returns the contents of standard output</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns></returns>
    private static bool TryExecute(string executable, string arguments, bool redirectInputOutput, out string result, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = 1000)
    {
      StringBuilder outputBuilder = new StringBuilder();
      using (System.Diagnostics.Process process = new System.Diagnostics.Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = redirectInputOutput } })
      {
        if (redirectInputOutput)
        {
          // Set UTF-8 encoding for standard output.
          process.StartInfo.StandardOutputEncoding = CONSOLE_ENCODING;
          // Enable raising events because Process does not raise events by default.
          process.EnableRaisingEvents = true;
          // Attach the event handler for OutputDataReceived before starting the process.
          process.OutputDataReceived += (sender, e) => outputBuilder.Append(e.Data);
        }
        process.Start();
        process.PriorityClass = priorityClass;

        if (redirectInputOutput)
          process.BeginOutputReadLine();

        if (process.WaitForExit(maxWaitMs))
        {
          result = RemoveEncodingPreamble(outputBuilder.ToString());
          return process.ExitCode == 0;
        }
        if (!process.HasExited)
          process.Kill();
      }
      result = null;
      return false;
    }

    /// <summary>
    /// Helper method to remove an existing encoding preamble (<see cref="Encoding.GetPreamble"/>) from the given <paramref name="rawString"/>.
    /// </summary>
    /// <param name="rawString">Raw string that might include the preamble (BOM).</param>
    /// <returns>String without preamble.</returns>
    private static string RemoveEncodingPreamble(string rawString)
    {
      if (!string.IsNullOrWhiteSpace(rawString) && rawString.StartsWith(CONSOLE_ENCODING_PREAMBLE))
        return rawString.Substring(CONSOLE_ENCODING_PREAMBLE.Length);
      return rawString;
    }

    /// <summary>
    /// Executes the <paramref name="executable"/> and waits a maximum time of <paramref name="maxWaitMs"/> for completion. If the process doesn't end in 
    /// this time, it gets aborted. This method tries to impersonate the interactive user and run the process under its identity.
    /// </summary>
    /// <param name="executable">Program to execute</param>
    /// <param name="arguments">Program arguments</param>
    /// <param name="token">User token to run process</param>
    /// <param name="redirectInputOutput"><c>true</c> to redirect standard streams.</param>
    /// <param name="result">Returns the contents of standard output.</param>
    /// <param name="priorityClass">Process priority</param>
    /// <param name="maxWaitMs">Maximum time to wait for completion</param>
    /// <returns><c>true</c> if process was executed and finished correctly</returns>
    private static bool TryExecute_Impersonated(string executable, string arguments, IntPtr token, bool redirectInputOutput, out string result, ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal, int maxWaitMs = INFINITE)
    {
      SafeFileHandle inputHandle = null;
      SafeFileHandle outputHandle = null;
      SafeFileHandle errorHandle = null;
      result = null;
      StringBuilder resultSb = null;
      Thread outputThread = null;

      try
      {
        PROCESS_INFORMATION pi;
        if (!StartProcess(executable, arguments, token, redirectInputOutput, out pi, out inputHandle, out outputHandle, out errorHandle))
        {
          int hr = Marshal.GetLastWin32Error();
          throw new InvalidOperationException(string.Format("Failed to start process! hr: 0x{0:x08}", hr));
        }

        SetProcessPriority(pi.hProcess, priorityClass);
        if (redirectInputOutput && IsValid(outputHandle))
        {
          resultSb = new StringBuilder();
          outputThread = new Thread(OutputThread);
          outputThread.SetApartmentState(ApartmentState.STA);
          outputThread.Start(new ThreadArgs { OutputHandle = outputHandle, OutputString = resultSb });
        }

        ProcessWaitHandle waitable = new ProcessWaitHandle(pi.hProcess);
        if (waitable.WaitOne(maxWaitMs))
        {
          uint exitCode;
          if (resultSb != null)
            result = resultSb.ToString();
          //if (redirectInputOutput && IsValid(outputHandle))
          //{
          //  const int bufferSize = 0x1000;
          //  using (StreamReader reader = new StreamReader(new FileStream(outputHandle, FileAccess.Read, bufferSize, false), CONSOLE_ENCODING, true, bufferSize))
          //  {
          //    while (!reader.EndOfStream)
          //      result = reader.ReadLine();
          //  }
          //}
          return GetExitCodeProcess(pi.hProcess, out exitCode) && exitCode == 0;
        }
        else
        {
          TerminateProcess(pi.hProcess, 255);
        }
        return false;
      }
      finally
      {
        if (outputThread != null) 
          outputThread.Abort();
        ImpersonationHelper.SafeCloseHandle(token);
        SafeCloseHandle(inputHandle);
        //SafeCloseHandle(outputHandle);
        SafeCloseHandle(errorHandle);
      }
    }

    class ThreadArgs
    {
      public SafeFileHandle OutputHandle;
      public StringBuilder OutputString;
    }

    /// <summary>
    /// The OutputThread ThreadStart delegate
    /// </summary>
    private static void OutputThread(object threadArgs)
    {
      ThreadArgs args = threadArgs as ThreadArgs;
      if (args == null)
        throw new ArgumentException("Invalid thread arguments.");

      const int bufferSize = 0x1000;
      try
      {
        using (StreamReader reader = new StreamReader(new FileStream(args.OutputHandle, FileAccess.Read, bufferSize, false), CONSOLE_ENCODING, true, bufferSize))
        {
          args.OutputString.Append(reader.ReadToEnd());
        }
      }
      catch (ThreadAbortException) { }
      catch (Exception e)
      {

      }
      finally
      {
        SafeCloseHandle(args.OutputHandle);
      }
    }

    private static bool StartProcess(string executable, string argument, IntPtr token, bool redirectInputOutput, out PROCESS_INFORMATION pi,
      out SafeFileHandle inputHandle, out SafeFileHandle outputHandle, out SafeFileHandle errorHandle)
    {
      inputHandle = null;
      outputHandle = null;
      errorHandle = null;
      SECURITY_ATTRIBUTES saProcess = new SECURITY_ATTRIBUTES();
      SECURITY_ATTRIBUTES saThread = new SECURITY_ATTRIBUTES();

      STARTUPINFO si = new STARTUPINFO
        {
          // lpDesktop = @"WinSta0\Default", // Modify as needed
          wShowWindow = SW_HIDE
        };

      if (redirectInputOutput)
      {
        si.dwFlags |= STARTF_USESTDHANDLES;
        CreatePipe(out outputHandle, out si.hStdOutput, false);
      }

      return CreateProcessAsUser(
        token,
        null,
        executable + " " + argument,
        saProcess,
        saThread,
        false,
        CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
        IntPtr.Zero,
        null,
        si,
        out pi);
    }

    private static bool SetProcessPriority(IntPtr processHandle, ProcessPriorityClass priority)
    {
      return SetPriorityClass(processHandle, (uint) priority); // Note: Enum values are equal to unmanaged constants.
    }

    public static void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
    {
      SECURITY_ATTRIBUTES lpPipeAttributes = new SECURITY_ATTRIBUTES { bInheritHandle = true };
      SafeFileHandle hWritePipe = null;
      try
      {
        if (parentInputs)
          CreatePipeWithSecurityAttributes(out childHandle, out hWritePipe, lpPipeAttributes, 4096);
        else
          CreatePipeWithSecurityAttributes(out hWritePipe, out childHandle, lpPipeAttributes, 4096);

        IntPtr hSourceProcessHandle = GetCurrentProcess();
        if (!DuplicateHandle(hSourceProcessHandle, hWritePipe, hSourceProcessHandle, out parentHandle, 0, false, 2))
          throw new Exception();
      }
      finally
      {
        SafeCloseHandle(hWritePipe);
      }
    }

    internal static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
    {
      if ((!CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize) || !IsValid(hReadPipe) || !IsValid(hWritePipe)))
        throw new Exception();
    }

    internal static bool IsValid(SafeFileHandle handle)
    {
      return handle != null && !handle.IsInvalid;
    }

    internal static void SafeCloseHandle(SafeFileHandle handle)
    {
      if (IsValid(handle))
        handle.Close();
    }
    #endregion
  }
}
