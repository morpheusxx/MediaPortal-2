using System.Diagnostics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace MpvSkiaPoc;

/// <summary>
/// Render loop per frame:
///   1. mpv renders the current video frame into an offscreen GL texture (only when a new frame is due)
///   2. Skia composites UI + video texture onto the window's default framebuffer
///   3. Silk.NET swaps buffers (vsync), mpv is told about the swap
/// Both libraries share one GL context, so GL state is reset at every hand-over.
/// </summary>
internal sealed class App
{
    private const uint GL_RGBA8 = 0x8058;

    private readonly AppOptions _opt;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly FrameStats _stats = new();

    private IWindow _window = null!;
    private GL _gl = null!;
    private GRContext? _gr;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private Vector2D<int> _surfaceSize;
    private MpvPlayer? _mpv;
    private VideoTarget? _video;
    private SKImage? _videoImage;
    private SceneRenderer? _scene;

    private string _glRenderer = "?", _glVersion = "?";
    private MpvSnapshot _snap = MpvSnapshot.Empty;
    private string[] _statLines = Array.Empty<string>();
    private double _lastPoll = -1, _lastLog;
    private bool _gotFirstFrame;

    // UI state
    private bool _homeMode, _showOsd = true, _showStats = true;
    private float _modeT, _focusPos;
    private int _focus;

    public App(AppOptions opt) => _opt = opt;

    public void Run()
    {
        var o = WindowOptions.Default;
        o.Size = new Vector2D<int>(1600, 900);
        o.Title = "MP2 cross-platform PoC - libmpv + SkiaSharp + Silk.NET";
        o.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible,
            new APIVersion(3, 3));
        o.VSync = true;
        o.PreferredStencilBufferBits = 8;
        o.FramesPerSecond = 0;   // render as fast as vsync allows
        o.UpdatesPerSecond = 0;
        o.WindowState = _opt.Fullscreen ? WindowState.Fullscreen : WindowState.Normal;

        _window = Window.Create(o);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClosing;
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        _glRenderer = _gl.GetStringS(StringName.Renderer) ?? "?";
        _glVersion = _gl.GetStringS(StringName.Version) ?? "?";
        Console.WriteLine($"OpenGL {_glVersion}");
        Console.WriteLine($"Renderer {_glRenderer}");

        var ctx = _window.GLContext ?? throw new InvalidOperationException("No OpenGL context");
        IntPtr GetProc(string name)
        {
            try { return ctx.GetProcAddress(name); }
            catch { return IntPtr.Zero; }
        }

        var glInterface = GRGlInterface.Create(GetProc)
                          ?? throw new InvalidOperationException("GRGlInterface.Create failed");
        if (!glInterface.Validate()) Console.Error.WriteLine("Warning: Skia GL interface validation failed");
        _gr = GRContext.CreateGl(glInterface) ?? throw new InvalidOperationException("GRContext.CreateGl failed");

        _scene = new SceneRenderer();
        _video = new VideoTarget(_gl);

        _mpv = new MpvPlayer(_opt.MpvOptions, _opt.BlockForTargetTime);
        _mpv.InitRender(GetProc);
        _mpv.Command("loadfile", _opt.Media);

        var input = _window.CreateInput();
        foreach (var kb in input.Keyboards) kb.KeyDown += OnKeyDown;

        Console.WriteLine("Keys: Space pause | Left/Right seek 10s | Up/Down seek 60s or menu focus | " +
                          "Enter/M home<->fullscreen | O osd | S stats | F fullscreen | Q/Esc quit");
    }

    private void OnRender(double dt)
    {
        if (_mpv is null || _gr is null || _video is null || _scene is null) return;
        double now = _clock.Elapsed.TotalSeconds;

        _mpv.ReportSwap(); // refers to the swap at the end of the previous frame
        if (_mpv.PumpEvents()) { _window.Close(); return; }

        var fb = _window.FramebufferSize;
        if (fb.X <= 0 || fb.Y <= 0) return; // minimised

        // Frame-rate independent animations
        float kMode = 1f - MathF.Exp(-(float)dt * 9f);
        float kFocus = 1f - MathF.Exp(-(float)dt * 16f);
        _modeT += ((_homeMode ? 1f : 0f) - _modeT) * kMode;
        _focusPos += (_focus - _focusPos) * kFocus;

        // ---- 1. video -> offscreen texture
        long t0 = Stopwatch.GetTimestamp();
        PrepareGlStateForMpv();
        bool recreated = _video.Ensure(fb.X, fb.Y);
        if (recreated)
        {
            _videoImage?.Dispose();
            _videoImage = null;
        }
        bool newFrame = _mpv.Render(_video.Fbo, _video.Width, _video.Height, force: recreated);
        if (newFrame) _gotFirstFrame = true;
        long t1 = Stopwatch.GetTimestamp();

        // ---- 2. Skia composition
        _gr.ResetContext(); // mpv touched GL state behind Skia's back
        EnsureSurface(fb);
        _videoImage ??= _video.CreateImage(_gr);

        if (now - _lastPoll > 0.25)
        {
            _lastPoll = now;
            _snap = _mpv.Poll();
            _statLines = BuildStatLines(now);
        }

        var state = new SceneState(now, _modeT, _focusPos, _focus, _showOsd, _showStats, _snap, _statLines);
        var canvas = _surface!.Canvas;
        _scene.Draw(canvas, fb.X, fb.Y, _gotFirstFrame ? _videoImage : null, state);
        canvas.Flush();
        _gr.Flush();
        long t2 = Stopwatch.GetTimestamp();

        _stats.Frame(now, Ms(t1 - t0), Ms(t2 - t1), newFrame);
        // ---- 3. Silk.NET swaps buffers after this callback returns
    }

    /// <summary>Put GL into a neutral state before handing the context to mpv.</summary>
    private void PrepareGlStateForMpv()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.StencilTest);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.CullFace);
        _gl.ColorMask(true, true, true, true);
        _gl.UseProgram(0);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 4);
    }

    private void EnsureSurface(Vector2D<int> fb)
    {
        if (_surface != null && fb == _surfaceSize) return;
        _surface?.Dispose();
        _renderTarget?.Dispose();

        var fbInfo = new GRGlFramebufferInfo(0, GL_RGBA8);
        _renderTarget = new GRBackendRenderTarget(fb.X, fb.Y, 0, 8, fbInfo);
        _surface = SKSurface.Create(_gr, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
                   ?? throw new InvalidOperationException("SKSurface.Create failed");
        _surfaceSize = fb;
    }

    private string[] BuildStatLines(double now)
    {
        var s = _stats.Compute();
        string renderer = _glRenderer.Length > 58 ? _glRenderer[..58] + "..." : _glRenderer;

        if (now - _lastLog >= 5)
        {
            _lastLog = now;
            Console.WriteLine(
                $"[stats] {s.Fps:0.0} fps | interval avg {s.AvgMs:0.00} med {s.MedianMs:0.00} max {s.MaxMs:0.00} ms | " +
                $"hitches {s.Hitches}/{s.Samples} | video {s.VideoFps:0.0} fps | hwdec {_snap.Hwdec} | " +
                $"drops vo {_snap.FrameDrops} dec {_snap.DecoderDrops} | cpu mpv {s.MpvMs:0.00} skia {s.SkiaMs:0.00} ms");
        }

        return new[]
        {
            $"GL     {renderer}",
            $"       {_glVersion}",
            $"Video  {_snap.Codec}",
            $"       {_snap.Size}  {_snap.PixFmt}  gamma {_snap.Gamma}",
            $"hwdec  {_snap.Hwdec}   src fps {_snap.Fps}   sync {_snap.VideoSync}",
            $"Render {s.Fps,5:0.0} fps  interval avg {s.AvgMs:0.00} / max {s.MaxMs:0.00} ms",
            $"       hitches {s.Hitches} of {s.Samples} (>1.5x median {s.MedianMs:0.00} ms)",
            $"Video  {s.VideoFps,5:0.0} new frames/s  drops vo {_snap.FrameDrops} / dec {_snap.DecoderDrops}",
            $"CPU    mpv {s.MpvMs:0.00} ms  skia {s.SkiaMs:0.00} ms per frame (submit)",
            "Keys   Space Left/Right Up/Down Enter O S F Q",
        };
    }

    private void OnKeyDown(IKeyboard kb, Key key, int scancode)
    {
        switch (key)
        {
            case Key.Space: _mpv?.Command("cycle", "pause"); break;
            case Key.Left: if (!_homeMode) _mpv?.Command("seek", "-10", "relative"); break;
            case Key.Right: if (!_homeMode) _mpv?.Command("seek", "10", "relative"); break;
            case Key.Up:
                if (_homeMode) _focus = Math.Max(0, _focus - 1);
                else _mpv?.Command("seek", "60", "relative");
                break;
            case Key.Down:
                if (_homeMode) _focus = Math.Min(SceneRenderer.MenuCount - 1, _focus + 1);
                else _mpv?.Command("seek", "-60", "relative");
                break;
            case Key.Enter:
            case Key.M:
                _homeMode = !_homeMode; break;
            case Key.O: _showOsd = !_showOsd; break;
            case Key.S: _showStats = !_showStats; break;
            case Key.F:
            case Key.F11:
                _window.WindowState = _window.WindowState == WindowState.Fullscreen
                    ? WindowState.Normal
                    : WindowState.Fullscreen;
                break;
            case Key.Escape:
            case Key.Q:
                _window.Close(); break;
        }
    }

    private void OnClosing()
    {
        _videoImage?.Dispose();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _mpv?.Dispose();      // frees the mpv render context while GL is still current
        _video?.Dispose();
        _scene?.Dispose();
        _gr?.Dispose();
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
