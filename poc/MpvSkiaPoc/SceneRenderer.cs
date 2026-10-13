using SkiaSharp;

namespace MpvSkiaPoc;

internal readonly record struct SceneState(
    double Time,
    float ModeT,          // 0 = fullscreen video, 1 = home screen with video preview
    float FocusPos,       // animated focus position in the menu
    int FocusIndex,
    bool ShowOsd,
    bool ShowStats,
    MpvSnapshot Snap,
    IReadOnlyList<string> StatsLines);

/// <summary>
/// Draws a skin-like UI with SkiaSharp. Stands in for what an MP2 SkinEngine Skia backend
/// would emit from MPF controls: gradients, rounded clips, text, shadows, blur, animation,
/// and video used as a brush (fullscreen, preview tile, reflection, frosted-glass OSD).
/// Layout uses a virtual 1080p coordinate space, like MP2 skins.
/// </summary>
internal sealed class SceneRenderer : IDisposable
{
    private static readonly string[] Menu =
        { "Videos", "Series", "Movies", "Music", "Pictures", "Online Videos", "Settings" };
    public static int MenuCount => Menu.Length;

    private static readonly SKColor Accent1 = new(0x2E, 0x8B, 0xFF);
    private static readonly SKColor Accent2 = new(0x9B, 0x4D, 0xFF);

    private readonly SKFont _title, _body, _small, _mono, _clock;
    private readonly SKPaint _p = new() { IsAntialias = true };
    private readonly SKPaint _measurePaint = new() { IsAntialias = true, SubpixelText = true };
    private readonly SKPaint _videoPaint = new() { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
    private readonly SKPaint _blurVideoPaint = new()
    {
        FilterQuality = SKFilterQuality.Low,
        ImageFilter = SKImageFilter.CreateBlur(28, 28),
    };
    private readonly SKMaskFilter _shadowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 22);
    private readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14);

    public SceneRenderer()
    {
        var regular = PickTypeface(SKFontStyle.Normal, "Segoe UI", "Noto Sans", "DejaVu Sans");
        var bold = PickTypeface(SKFontStyle.Bold, "Segoe UI", "Noto Sans", "DejaVu Sans");
        var mono = PickTypeface(SKFontStyle.Normal, "Cascadia Mono", "Consolas", "DejaVu Sans Mono", "Liberation Mono");

        _title = MakeFont(bold, 38);
        _clock = MakeFont(regular, 40);
        _body = MakeFont(regular, 30);
        _small = MakeFont(regular, 24);
        _mono = MakeFont(mono, 19);
    }

    private static SKTypeface PickTypeface(SKFontStyle style, params string[] families)
    {
        foreach (var f in families)
        {
            var tf = SKTypeface.FromFamilyName(f, style);
            if (tf != null && string.Equals(tf.FamilyName, f, StringComparison.OrdinalIgnoreCase)) return tf;
        }
        return SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
    }

    private static SKFont MakeFont(SKTypeface tf, float size) =>
        new(tf, size) { Edging = SKFontEdging.Antialias, Subpixel = true };

    public void Draw(SKCanvas c, int pixelW, int pixelH, SKImage? video, in SceneState st)
    {
        float s = Math.Min(pixelW / 1920f, pixelH / 1080f);
        float W = pixelW / s, H = pixelH / s;
        float e = Smooth(Math.Clamp(st.ModeT, 0f, 1f));

        c.Clear(SKColors.Black);
        c.Save();
        c.Scale(s);

        if (e > 0.001f) DrawBackground(c, W, H, st.Time);

        var full = new SKRect(0, 0, W, H);
        float pw = W * 0.50f, ph = pw * H / W;
        var pip = SKRect.Create(W * 0.44f, H * 0.17f, pw, ph);
        var vr = Lerp(full, pip, e);

        if (video != null) DrawVideo(c, video, vr, 22f * e, e, H);
        else DrawCentered(c, "Waiting for first video frame ...", W / 2, H / 2, _body, SKColors.White);

        if (e > 0.001f) DrawMenu(c, H, e, st);
        if (st.ShowOsd)
        {
            DrawTopBar(c, W, e, st);
            if (e < 0.999f) DrawOsd(c, video, vr, W, H, 1f - e, st);
        }
        if (st.ShowStats) DrawStats(c, W, st);

        c.Restore();
    }

    // ---------------------------------------------------------------- pieces

    private void DrawBackground(SKCanvas c, float W, float H, double time)
    {
        using (var g = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, H),
                   new[] { new SKColor(8, 14, 34), new SKColor(26, 10, 44) }, null, SKShaderTileMode.Clamp))
        {
            _p.Shader = g;
            c.DrawRect(0, 0, W, H, _p);
            _p.Shader = null;
        }

        float t = (float)time;
        for (int i = 0; i < 5; i++)
        {
            float cx = W * (0.5f + 0.42f * MathF.Sin(t * 0.13f * (i + 1) + i * 1.7f));
            float cy = H * (0.5f + 0.36f * MathF.Cos(t * 0.11f * (i + 2) + i));
            float r = H * (0.25f + 0.05f * i);
            var col = i % 2 == 0 ? Accent1 : Accent2;
            using var rg = SKShader.CreateRadialGradient(new SKPoint(cx, cy), r,
                new[] { col.WithAlpha(55), col.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
            _p.Shader = rg;
            c.DrawCircle(cx, cy, r, _p);
            _p.Shader = null;
        }
    }

    private void DrawVideo(SKCanvas c, SKImage video, SKRect vr, float radius, float e, float H)
    {
        var src = new SKRect(0, 0, video.Width, video.Height);

        if (e > 0.001f)
        {
            _p.Color = new SKColor(0, 0, 0, (byte)(170 * e));
            _p.MaskFilter = _shadowBlur;
            var shadow = vr;
            shadow.Offset(0, 16);
            c.DrawRoundRect(shadow, radius, radius, _p);
            _p.MaskFilter = null;
        }

        c.Save();
        c.ClipRoundRect(new SKRoundRect(vr, radius, radius), antialias: true);
        c.DrawImage(video, src, vr, _videoPaint);
        c.Restore();

        if (e <= 0.001f) return;

        // Reflection: the same video texture drawn a second time, mirrored and faded.
        const float gap = 10f;
        float mirror = vr.Bottom + gap / 2;
        var refl = new SKRect(vr.Left, vr.Bottom + gap, vr.Right, Math.Min(vr.Bottom + gap + vr.Height * 0.30f, H));
        if (refl.Height < 1) return;

        c.SaveLayer(refl, null);
        c.Save();
        c.Translate(0, 2 * mirror);
        c.Scale(1, -1);
        c.ClipRoundRect(new SKRoundRect(vr, radius, radius), antialias: true);
        c.DrawImage(video, src, vr, _videoPaint);
        c.Restore();
        using (var fade = SKShader.CreateLinearGradient(new SKPoint(0, refl.Top), new SKPoint(0, refl.Bottom),
                   new[] { new SKColor(0, 0, 0, (byte)(100 * e)), new SKColor(0, 0, 0, 0) }, null,
                   SKShaderTileMode.Clamp))
        using (var maskPaint = new SKPaint { Shader = fade, BlendMode = SKBlendMode.DstIn })
        {
            c.DrawRect(refl, maskPaint);
        }
        c.Restore();
    }

    private void DrawMenu(SKCanvas c, float H, float e, in SceneState st)
    {
        float x = 70 - (1 - e) * 460;
        float tileW = 560, tileH = 88, gap = 14, top = H * 0.17f;
        byte a = (byte)(255 * e);

        _p.Color = SKColors.White.WithAlpha(a);
        c.DrawText("MediaPortal 2 \u00b7 cross-platform PoC", x, top - 34, _title, _p);

        for (int i = 0; i < Menu.Length; i++)
        {
            var r = SKRect.Create(x, top + i * (tileH + gap), tileW, tileH);
            _p.Color = new SKColor(255, 255, 255, (byte)(20 * e));
            c.DrawRoundRect(r, 14, 14, _p);
        }

        // Animated focus: smooth movement + pulsing glow
        float fy = top + st.FocusPos * (tileH + gap);
        float pulse = 0.5f + 0.5f * MathF.Sin((float)st.Time * 3f);
        var focus = SKRect.Create(x, fy, tileW, tileH);
        var glow = focus;
        glow.Inflate(6, 6);
        _p.Color = Accent1.WithAlpha((byte)((90 + 90 * pulse) * e));
        _p.MaskFilter = _glowBlur;
        c.DrawRoundRect(glow, 18, 18, _p);
        _p.MaskFilter = null;
        using (var fg = SKShader.CreateLinearGradient(new SKPoint(focus.Left, 0), new SKPoint(focus.Right, 0),
                   new[] { Accent1.WithAlpha(a), Accent2.WithAlpha(a) }, null, SKShaderTileMode.Clamp))
        {
            _p.Shader = fg;
            c.DrawRoundRect(focus, 14, 14, _p);
            _p.Shader = null;
        }

        for (int i = 0; i < Menu.Length; i++)
        {
            var r = SKRect.Create(x, top + i * (tileH + gap), tileW, tileH);
            _p.Color = SKColors.White.WithAlpha(i == st.FocusIndex ? a : (byte)(190 * e));
            c.DrawText(Menu[i], r.Left + 30, r.MidY + 11, _body, _p);
        }
    }

    private void DrawTopBar(SKCanvas c, float W, float e, in SceneState st)
    {
        using (var g = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 150),
                   new[] { new SKColor(0, 0, 0, 180), new SKColor(0, 0, 0, 0) }, null, SKShaderTileMode.Clamp))
        {
            _p.Shader = g;
            c.DrawRect(0, 0, W, 150, _p);
            _p.Shader = null;
        }

        if (e < 0.999f && st.Snap.Title.Length > 0)
        {
            _p.Color = SKColors.White.WithAlpha((byte)(255 * (1 - e)));
            c.DrawText(Ellipsize(st.Snap.Title, _title, W * 0.6f), 56, 74, _title, _p);
        }

        string clock = DateTime.Now.ToString("HH:mm");
        _p.Color = SKColors.White;
        c.DrawText(clock, W - 56 - Measure(_clock, clock), 74, _clock, _p);
    }

    private void DrawOsd(SKCanvas c, SKImage? video, SKRect videoRect, float W, float H, float alpha, in SceneState st)
    {
        var panel = SKRect.Create(56, H - 56 - 150, W - 112, 150);
        var rr = new SKRoundRect(panel, 22, 22);

        if (alpha < 0.999f)
        {
            using var lp = new SKPaint { Color = new SKColor(0, 0, 0, (byte)(255 * alpha)) };
            var bounds = panel;
            bounds.Inflate(4, 4);
            c.SaveLayer(bounds, lp);
        }
        else c.Save();

        // Frosted glass: blurred video behind the panel (video used as a brush again)
        if (video != null)
        {
            c.Save();
            c.ClipRoundRect(rr, antialias: true);
            c.DrawImage(video, new SKRect(0, 0, video.Width, video.Height), videoRect, _blurVideoPaint);
            c.Restore();
        }
        _p.Color = new SKColor(10, 12, 20, 140);
        c.DrawRoundRect(rr, _p);
        _p.Style = SKPaintStyle.Stroke;
        _p.StrokeWidth = 1.5f;
        _p.Color = new SKColor(255, 255, 255, 45);
        c.DrawRoundRect(rr, _p);
        _p.Style = SKPaintStyle.Fill;

        // Play/pause glyph
        float ix = panel.Left + 40, iy = panel.Top + 50;
        _p.Color = SKColors.White;
        if (st.Snap.Paused)
        {
            using var path = new SKPath();
            path.MoveTo(ix, iy - 20);
            path.LineTo(ix + 34, iy);
            path.LineTo(ix, iy + 20);
            path.Close();
            c.DrawPath(path, _p);
        }
        else
        {
            c.DrawRoundRect(SKRect.Create(ix, iy - 20, 11, 40), 3, 3, _p);
            c.DrawRoundRect(SKRect.Create(ix + 20, iy - 20, 11, 40), 3, 3, _p);
        }

        string time = $"{Fmt(st.Snap.Position)} / {Fmt(st.Snap.Duration)}";
        float tw = Measure(_small, time);
        c.DrawText(time, panel.Right - 40 - tw, iy + 10, _small, _p);
        c.DrawText(Ellipsize(st.Snap.Title, _body, panel.Width - tw - 200), ix + 70, iy + 11, _body, _p);

        // Progress bar
        var track = SKRect.Create(panel.Left + 40, panel.Bottom - 46, panel.Width - 80, 10);
        _p.Color = new SKColor(255, 255, 255, 55);
        c.DrawRoundRect(track, 5, 5, _p);
        float frac = st.Snap.Duration is > 0 && st.Snap.Position is { } pos
            ? Math.Clamp((float)(pos / st.Snap.Duration.Value), 0f, 1f) : 0f;
        if (frac > 0)
        {
            var fill = SKRect.Create(track.Left, track.Top, track.Width * frac, track.Height);
            using var g = SKShader.CreateLinearGradient(new SKPoint(track.Left, 0), new SKPoint(track.Right, 0),
                new[] { Accent1, Accent2 }, null, SKShaderTileMode.Clamp);
            _p.Shader = g;
            c.DrawRoundRect(fill, 5, 5, _p);
            _p.Shader = null;
            _p.Color = SKColors.White;
            c.DrawCircle(fill.Right, track.MidY, 11, _p);
        }

        c.Restore();
    }

    private void DrawStats(SKCanvas c, float W, in SceneState st)
    {
        const float lh = 25, pw = 760;
        var lines = st.StatsLines;
        var r = SKRect.Create(W - pw - 40, 120, pw, lines.Count * lh + 76);

        _p.Color = new SKColor(0, 0, 0, 175);
        c.DrawRoundRect(r, 14, 14, _p);

        _p.Color = new SKColor(235, 235, 235);
        for (int i = 0; i < lines.Count; i++)
            c.DrawText(lines[i], r.Left + 20, r.Top + 34 + i * lh, _mono, _p);

        // Smoothness ticker: constant-speed marker, stutter is directly visible
        var track = SKRect.Create(r.Left + 20, r.Bottom - 30, r.Width - 40, 6);
        _p.Color = new SKColor(255, 255, 255, 50);
        c.DrawRoundRect(track, 3, 3, _p);
        float phase = (float)(st.Time * 0.5 % 1.0);
        _p.Color = Accent1;
        c.DrawRect(SKRect.Create(track.Left + phase * (track.Width - 14), track.Top - 6, 14, 18), _p);
    }

    // ---------------------------------------------------------------- helpers

    private void DrawCentered(SKCanvas c, string text, float cx, float cy, SKFont font, SKColor color)
    {
        _p.Color = color;
        c.DrawText(text, cx - Measure(font, text) / 2, cy, font, _p);
    }

    private string Ellipsize(string text, SKFont font, float maxWidth)
    {
        if (Measure(font, text) <= maxWidth) return text;
        for (int len = text.Length - 1; len > 0; len--)
        {
            string t = text[..len] + "\u2026";
            if (Measure(font, t) <= maxWidth) return t;
        }
        return "";
    }

    /// <summary>SKFont.MeasureText has no string overload in SkiaSharp 2.88; SKPaint's does.</summary>
    private float Measure(SKFont font, string text)
    {
        _measurePaint.Typeface = font.Typeface;
        _measurePaint.TextSize = font.Size;
        return _measurePaint.MeasureText(text);
    }

    private static string Fmt(double? seconds)
    {
        if (seconds is not { } sec || double.IsNaN(sec)) return "--:--";
        var ts = TimeSpan.FromSeconds(Math.Max(0, sec));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    private static SKRect Lerp(SKRect a, SKRect b, float t) => new(
        a.Left + (b.Left - a.Left) * t, a.Top + (b.Top - a.Top) * t,
        a.Right + (b.Right - a.Right) * t, a.Bottom + (b.Bottom - a.Bottom) * t);

    public void Dispose()
    {
        _p.Dispose();
        _measurePaint.Dispose();
        _videoPaint.Dispose();
        _blurVideoPaint.Dispose();
        _shadowBlur.Dispose();
        _glowBlur.Dispose();
        _title.Dispose();
        _body.Dispose();
        _small.Dispose();
        _mono.Dispose();
        _clock.Dispose();
    }
}
