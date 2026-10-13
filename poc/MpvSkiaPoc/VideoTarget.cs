using Silk.NET.OpenGL;
using SkiaSharp;

namespace MpvSkiaPoc;

/// <summary>
/// GL texture + FBO that mpv renders into. Skia wraps the texture as an SKImage,
/// so the UI can draw video anywhere, scaled, clipped, blurred or mirrored.
/// This is the cross-platform counterpart to MP2's video surface / video brush.
/// </summary>
internal sealed unsafe class VideoTarget : IDisposable
{
    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const uint GL_RGBA8 = 0x8058;

    private readonly GL _gl;
    public uint Fbo { get; private set; }
    public uint Texture { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public VideoTarget(GL gl) => _gl = gl;

    /// <summary>(Re)creates the target if the size changed. Returns true if recreated.</summary>
    public bool Ensure(int width, int height)
    {
        if (Fbo != 0 && width == Width && height == Height) return false;
        Delete();

        Texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, Texture);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        Fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, Texture, 0);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Video FBO incomplete: {status}");

        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        Width = width;
        Height = height;
        return true;
    }

    /// <summary>Wraps the GL texture (no copy). Recreate after <see cref="Ensure"/> returns true.</summary>
    public SKImage CreateImage(GRContext gr)
    {
        var info = new GRGlTextureInfo(GL_TEXTURE_2D, Texture, GL_RGBA8);
        using var backendTexture = new GRBackendTexture(Width, Height, false, info);
        // mpv renders with flip-y = 0 into an FBO, i.e. image row 0 is the top row.
        return SKImage.FromTexture(gr, backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888)
               ?? throw new InvalidOperationException("SKImage.FromTexture failed");
    }

    private void Delete()
    {
        if (Fbo != 0) _gl.DeleteFramebuffer(Fbo);
        if (Texture != 0) _gl.DeleteTexture(Texture);
        Fbo = 0;
        Texture = 0;
    }

    public void Dispose() => Delete();
}
