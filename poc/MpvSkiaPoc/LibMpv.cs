using System.Reflection;
using System.Runtime.InteropServices;

namespace MpvSkiaPoc.Interop;

/// <summary>
/// Minimal P/Invoke surface for libmpv's client API (client.h) and
/// OpenGL render API (render.h / render_gl.h). Only what the PoC needs.
/// </summary>
internal static unsafe class LibMpv
{
    public const string Lib = "mpv";

    // mpv_format
    public const int FormatFlag = 3;
    public const int FormatDouble = 5;

    // mpv_render_param_type
    public const int RenderParamApiType = 1;
    public const int RenderParamOpenGLInitParams = 2;
    public const int RenderParamOpenGLFbo = 3;
    public const int RenderParamFlipY = 4;
    public const int RenderParamBlockForTargetTime = 12;

    // mpv_render_update_flag
    public const ulong RenderUpdateFrame = 1;

    // mpv_event_id / mpv_end_file_reason
    public const int EventNone = 0;
    public const int EventShutdown = 1;
    public const int EventEndFile = 7;
    public const int EndFileReasonError = 4;

    /// <summary>Maps the logical name "mpv" to the platform-specific libmpv binary.</summary>
    public static void InstallResolver()
    {
        NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly, Resolve);
    }

    private static IntPtr Resolve(string name, Assembly asm, DllImportSearchPath? path)
    {
        if (name != Lib) return IntPtr.Zero;

        string[] candidates =
            OperatingSystem.IsWindows() ? new[] { "libmpv-2.dll", "mpv-2.dll", "mpv-1.dll" } :
            OperatingSystem.IsMacOS() ? new[] { "libmpv.2.dylib", "libmpv.dylib" } :
            new[] { "libmpv.so.2", "libmpv.so.1", "libmpv.so" };

        foreach (var c in candidates)
        {
            var local = Path.Combine(AppContext.BaseDirectory, c);
            if (File.Exists(local) && NativeLibrary.TryLoad(local, out var h1)) return h1;
            if (NativeLibrary.TryLoad(c, asm, path, out var h2)) return h2;
        }
        return IntPtr.Zero; // -> DllNotFoundException with a clear message in Program.cs
    }

    // ---------- client API ----------

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_option_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    /// <param name="args">NULL-terminated array of UTF-8 C strings.</param>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr[] args);

    [DllImport(Lib, EntryPoint = "mpv_get_property", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property_double(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out double data);

    [DllImport(Lib, EntryPoint = "mpv_get_property", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property_int(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out int data);

    /// <returns>char* that must be released with mpv_free, or NULL.</returns>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_error_string(int error);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    // ---------- render API ----------

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, MpvRenderParam* parameters);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(IntPtr ctx, IntPtr callback, IntPtr callbackCtx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(IntPtr ctx, MpvRenderParam* parameters);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_report_swap(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr ctx);

    public static string ErrorString(int error) =>
        Marshal.PtrToStringUTF8(mpv_error_string(error)) ?? $"error {error}";
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
    // Present in libmpv API 1.x (extra_exts), removed in 2.x. Kept NULL so both work.
    public IntPtr ExtraExts;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MpvOpenGLFbo
{
    public int Fbo;
    public int W;
    public int H;
    public int InternalFormat; // 0 = unknown/default
}
