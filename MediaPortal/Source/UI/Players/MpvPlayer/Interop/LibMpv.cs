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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MediaPortal.UI.Players.Mpv.Interop
{
  /// <summary>
  /// P/Invoke surface of libmpv's client API (<c>client.h</c>) and OpenGL render API (<c>render.h</c>, <c>render_gl.h</c>).
  /// </summary>
  internal static unsafe class LibMpv
  {
    public const string LIB = "mpv";

    // mpv_format
    public const int FORMAT_FLAG = 3;
    public const int FORMAT_INT64 = 4;
    public const int FORMAT_DOUBLE = 5;

    // mpv_render_param_type
    public const int RENDER_PARAM_API_TYPE = 1;
    public const int RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
    public const int RENDER_PARAM_OPENGL_FBO = 3;
    public const int RENDER_PARAM_FLIP_Y = 4;
    public const int RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12;

    // mpv_render_update_flag
    public const ulong RENDER_UPDATE_FRAME = 1;

    // mpv_event_id
    public const int EVENT_NONE = 0;
    public const int EVENT_SHUTDOWN = 1;
    public const int EVENT_LOG_MESSAGE = 2;
    public const int EVENT_START_FILE = 6;
    public const int EVENT_END_FILE = 7;
    public const int EVENT_FILE_LOADED = 8;
    public const int EVENT_PLAYBACK_RESTART = 21;

    // mpv_end_file_reason
    public const int END_FILE_REASON_EOF = 0;
    public const int END_FILE_REASON_STOP = 2;
    public const int END_FILE_REASON_QUIT = 3;
    public const int END_FILE_REASON_ERROR = 4;

    private static bool _resolverInstalled;

    /// <summary>
    /// Maps the logical library name "mpv" to the platform specific libmpv binary. Looks into <c>runtimes\&lt;rid&gt;\native</c> of the
    /// plugin directory first.
    /// </summary>
    public static void InstallResolver()
    {
      if (_resolverInstalled)
        return;
      _resolverInstalled = true;
      NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly, Resolve);
    }

    private static string CurrentRid
    {
      get
      {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
          Architecture.X86 => "x86",
          Architecture.Arm => "arm",
          Architecture.Arm64 => "arm64",
          _ => "x64"
        };
        return (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-") + arch;
      }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
      if (name != LIB)
        return IntPtr.Zero;

      string[] candidates =
          OperatingSystem.IsWindows() ? new[] { "libmpv-2.dll", "mpv-2.dll", "mpv-1.dll" } :
          OperatingSystem.IsMacOS() ? new[] { "libmpv.2.dylib", "libmpv.dylib" } :
          new[] { "libmpv.so.2", "libmpv.so.1", "libmpv.so" };

      string baseDirectory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
      foreach (string candidate in candidates)
      {
        foreach (string directory in new[] { Path.Combine(baseDirectory, "runtimes", CurrentRid, "native"), baseDirectory, AppContext.BaseDirectory })
        {
          string path = Path.Combine(directory, candidate);
          if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle))
            return handle;
        }
        // System wide installation
        if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out IntPtr systemHandle))
          return systemHandle;
      }
      return IntPtr.Zero;
    }

    #region Client API

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    /// <param name="args">NULL-terminated array of UTF-8 C strings.</param>
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr[] args);

    [DllImport(LIB, EntryPoint = "mpv_get_property", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property_double(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out double data);

    [DllImport(LIB, EntryPoint = "mpv_get_property", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property_int(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out int data);

    [DllImport(LIB, EntryPoint = "mpv_get_property", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property_long(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out long data);

    /// <returns>char* which must be released with <see cref="mpv_free"/>, or NULL.</returns>
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_error_string(int error);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_wakeup(IntPtr ctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_request_log_messages(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string minLevel);

    #endregion

    #region Render API

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, MpvRenderParam* parameters);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(IntPtr ctx, IntPtr callback, IntPtr callbackCtx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(IntPtr ctx, MpvRenderParam* parameters);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_report_swap(IntPtr ctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr ctx);

    #endregion

    public static string ErrorString(int error)
    {
      return Marshal.PtrToStringUTF8(mpv_error_string(error)) ?? "error " + error;
    }
  }

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  internal delegate IntPtr MpvGetProcAddressFn(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  internal delegate void MpvUpdateCallbackFn(IntPtr ctx);

  [StructLayout(LayoutKind.Sequential)]
  internal struct MpvRenderParam
  {
    public int Type;
    public IntPtr Data;

    public MpvRenderParam(int type, IntPtr data)
    {
      Type = type;
      Data = data;
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct MpvOpenGLInitParams
  {
    public IntPtr GetProcAddress;
    public IntPtr GetProcAddressCtx;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct MpvOpenGLFbo
  {
    public int Fbo;
    public int W;
    public int H;
    public int InternalFormat; // 0 = unknown/default
  }
}
