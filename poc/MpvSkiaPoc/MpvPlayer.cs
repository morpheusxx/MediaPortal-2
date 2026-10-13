using System.Globalization;
using System.Runtime.InteropServices;
using MpvSkiaPoc.Interop;

namespace MpvSkiaPoc;

internal sealed record MpvSnapshot(
    string Title, double? Position, double? Duration, bool Paused,
    string Hwdec, string Codec, string Size, string PixFmt, string Gamma,
    string Fps, string FrameDrops, string DecoderDrops, string VideoSync)
{
    public static readonly MpvSnapshot Empty =
        new("", null, null, false, "-", "-", "-", "-", "-", "-", "0", "0", "-");
}

/// <summary>
/// Wraps an mpv instance plus an OpenGL render context. In MP2 terms this is the
/// replacement for the DirectShow graph + EVR presenter: it produces video frames
/// into a GL texture that the UI layer composites like any other brush.
/// All render calls must happen on the thread that owns the GL context.
/// </summary>
internal sealed unsafe class MpvPlayer : IDisposable
{
    private readonly IntPtr _handle;
    private readonly bool _blockForTargetTime;
    private IntPtr _renderCtx;

    // Delegates handed to native code must stay referenced for the context lifetime.
    private MpvGetProcAddressFn? _getProcFn;
    private MpvUpdateCallbackFn? _updateFn;
    private Func<string, IntPtr>? _glGetProc;

    private int _updatePending = 1;

    public MpvPlayer(IEnumerable<KeyValuePair<string, string>> userOptions, bool blockForTargetTime)
    {
        _blockForTargetTime = blockForTargetTime;
        _handle = LibMpv.mpv_create();
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("mpv_create failed");

        var defaults = new (string, string)[]
        {
            ("vo", "libmpv"),               // frames go through the render API
            ("hwdec", "auto-safe"),         // check 'hwdec' line in the stats overlay
            ("terminal", "yes"),            // mpv logs to our console
            ("msg-level", "all=warn"),
            ("loop-file", "inf"),           // keep running for soak tests
            ("input-default-bindings", "no"),
            ("input-vo-keyboard", "no"),
            ("osc", "no"),
        };
        foreach (var (k, v) in defaults) SetOption(k, v);
        foreach (var kv in userOptions) SetOption(kv.Key, kv.Value);

        Check(LibMpv.mpv_initialize(_handle), "mpv_initialize");
    }

    public void SetOption(string name, string value)
    {
        int r = LibMpv.mpv_set_option_string(_handle, name, value);
        if (r < 0) Console.Error.WriteLine($"[mpv] option {name}={value}: {LibMpv.ErrorString(r)}");
    }

    /// <summary>Create the GL render context. The GL context must be current.</summary>
    public void InitRender(Func<string, IntPtr> glGetProcAddress)
    {
        _glGetProc = glGetProcAddress;
        _getProcFn = (_, name) => _glGetProc(name);
        _updateFn = _ => Interlocked.Exchange(ref _updatePending, 1);

        var init = new MpvOpenGLInitParams
        {
            GetProcAddress = Marshal.GetFunctionPointerForDelegate(_getProcFn),
        };
        IntPtr apiType = Marshal.StringToCoTaskMemUTF8("opengl");
        try
        {
            var p = stackalloc MpvRenderParam[3];
            p[0] = new MpvRenderParam(LibMpv.RenderParamApiType, apiType);
            p[1] = new MpvRenderParam(LibMpv.RenderParamOpenGLInitParams, (IntPtr)(&init));
            p[2] = default;
            Check(LibMpv.mpv_render_context_create(out _renderCtx, _handle, p), "mpv_render_context_create");
        }
        finally
        {
            Marshal.FreeCoTaskMem(apiType);
        }

        LibMpv.mpv_render_context_set_update_callback(
            _renderCtx, Marshal.GetFunctionPointerForDelegate(_updateFn), IntPtr.Zero);
    }

    /// <summary>
    /// Renders the current video frame into <paramref name="fbo"/> if mpv has a new one
    /// (or if <paramref name="force"/> is set, e.g. after the FBO was recreated).
    /// Returns true when a new video frame was rendered.
    /// </summary>
    public bool Render(uint fbo, int width, int height, bool force)
    {
        bool newFrame = false;
        if (Interlocked.Exchange(ref _updatePending, 0) == 1)
            newFrame = (LibMpv.mpv_render_context_update(_renderCtx) & LibMpv.RenderUpdateFrame) != 0;

        if (!newFrame && !force) return false;

        var target = new MpvOpenGLFbo { Fbo = (int)fbo, W = width, H = height, InternalFormat = 0 };
        int flipY = 0; // offscreen FBO: keep top-left origin, Skia wraps it as TopLeft
        int block = _blockForTargetTime ? 1 : 0;

        var p = stackalloc MpvRenderParam[4];
        p[0] = new MpvRenderParam(LibMpv.RenderParamOpenGLFbo, (IntPtr)(&target));
        p[1] = new MpvRenderParam(LibMpv.RenderParamFlipY, (IntPtr)(&flipY));
        p[2] = new MpvRenderParam(LibMpv.RenderParamBlockForTargetTime, (IntPtr)(&block));
        p[3] = default;

        int r = LibMpv.mpv_render_context_render(_renderCtx, p);
        if (r < 0) Console.Error.WriteLine($"[mpv] render: {LibMpv.ErrorString(r)}");
        return newFrame;
    }

    /// <summary>Tell mpv a buffer swap happened (improves its frame timing estimates).</summary>
    public void ReportSwap()
    {
        if (_renderCtx != IntPtr.Zero) LibMpv.mpv_render_context_report_swap(_renderCtx);
    }

    /// <summary>Drain mpv events. Returns true if mpv requested shutdown.</summary>
    public bool PumpEvents()
    {
        while (true)
        {
            IntPtr ev = LibMpv.mpv_wait_event(_handle, 0);
            int id = Marshal.ReadInt32(ev);
            switch (id)
            {
                case LibMpv.EventNone:
                    return false;
                case LibMpv.EventShutdown:
                    return true;
                case LibMpv.EventEndFile:
                    // mpv_event: int id; int error; uint64 reply_userdata; void* data  -> data at offset 16
                    IntPtr data = Marshal.ReadIntPtr(ev, 16);
                    if (data != IntPtr.Zero)
                    {
                        int reason = Marshal.ReadInt32(data);
                        int error = Marshal.ReadInt32(data, 4);
                        if (reason == LibMpv.EndFileReasonError)
                            Console.Error.WriteLine($"[mpv] playback failed: {LibMpv.ErrorString(error)}");
                    }
                    break;
            }
        }
    }

    public void Command(params string[] args)
    {
        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++) ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            int r = LibMpv.mpv_command(_handle, ptrs);
            if (r < 0) Console.Error.WriteLine($"[mpv] command '{string.Join(' ', args)}': {LibMpv.ErrorString(r)}");
        }
        finally
        {
            foreach (var p in ptrs) if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);
        }
    }

    public MpvSnapshot Poll()
    {
        string? w = GetString("video-params/w"), h = GetString("video-params/h");
        return new MpvSnapshot(
            Title: GetString("media-title") ?? "",
            Position: GetDouble("time-pos"),
            Duration: GetDouble("duration"),
            Paused: GetFlag("pause"),
            Hwdec: GetString("hwdec-current") ?? "no",
            Codec: GetString("video-codec") ?? "-",
            Size: w != null && h != null ? $"{w}x{h}" : "-",
            PixFmt: GetString("video-params/hw-pixelformat") ?? GetString("video-params/pixelformat") ?? "-",
            Gamma: GetString("video-params/gamma") ?? "-",
            Fps: Num(GetString("estimated-vf-fps") ?? GetString("container-fps")),
            FrameDrops: GetString("frame-drop-count") ?? "0",
            DecoderDrops: GetString("decoder-frame-drop-count") ?? "0",
            VideoSync: GetString("video-sync") ?? "-");
    }

    private static string Num(string? s) =>
        s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("0.###", CultureInfo.InvariantCulture)
            : "-";

    public double? GetDouble(string name) =>
        LibMpv.mpv_get_property_double(_handle, name, LibMpv.FormatDouble, out var v) >= 0 ? v : null;

    public bool GetFlag(string name) =>
        LibMpv.mpv_get_property_int(_handle, name, LibMpv.FormatFlag, out var v) >= 0 && v != 0;

    public string? GetString(string name)
    {
        IntPtr p = LibMpv.mpv_get_property_string(_handle, name);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { LibMpv.mpv_free(p); }
    }

    private static void Check(int result, string what)
    {
        if (result < 0) throw new InvalidOperationException($"{what} failed: {LibMpv.ErrorString(result)}");
    }

    public void Dispose()
    {
        // Render context must be freed while the GL context is still current, before the core.
        if (_renderCtx != IntPtr.Zero)
        {
            LibMpv.mpv_render_context_free(_renderCtx);
            _renderCtx = IntPtr.Zero;
        }
        LibMpv.mpv_terminate_destroy(_handle);
    }
}
