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
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.DirectX;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Gpu
{
  /// <summary>
  /// A render target. Drawing is done on <see cref="Canvas"/>. A surface either belongs to a <see cref="Texture"/> created with
  /// <see cref="Usage.RenderTarget"/> or is a stand-alone off-screen target (see <see cref="Create"/>) or the back buffer.
  /// </summary>
  public class Surface : IDisposable
  {
    protected readonly SKSurface _surface;
    protected readonly bool _ownsSurface;
    protected readonly Texture _ownerTexture;
    protected readonly int _width;
    protected readonly int _height;

    internal Surface(SKSurface surface, int width, int height, bool ownsSurface, Texture ownerTexture)
    {
      _surface = surface;
      _width = width;
      _height = height;
      _ownsSurface = ownsSurface;
      _ownerTexture = ownerTexture;
    }

    /// <summary>
    /// Creates a stand-alone GPU render target.
    /// </summary>
    public static Surface Create(int width, int height, Format format)
    {
      SKImageInfo info = new SKImageInfo(width, height, format.ToColorType(), SKAlphaType.Premul);
      SKSurface surface = GraphicsDevice.CreateGpuSurface(info);
      return new Surface(surface, width, height, true, null);
    }

    public int Width
    {
      get { return _width; }
    }

    public int Height
    {
      get { return _height; }
    }

    public SKCanvas Canvas
    {
      get { return _surface.Canvas; }
    }

    internal SKSurface SkSurface
    {
      get { return _surface; }
    }

    /// <summary>
    /// Takes an immutable snapshot of the current content. Cheap (copy on write).
    /// </summary>
    public SKImage Snapshot()
    {
      _surface.Canvas.Flush();
      return _surface.Snapshot();
    }

    public SurfaceDescription Description
    {
      get { return new SurfaceDescription { Width = _width, Height = _height, Format = Format.A8R8G8B8 }; }
    }

    public void MarkChanged()
    {
      if (_ownerTexture != null)
        _ownerTexture.InvalidateImage();
    }

    public virtual void Dispose()
    {
      if (_ownsSurface)
        _surface.Dispose();
    }
  }

  /// <summary>
  /// A texture which can be used as brush/image source. Either created from an image (decoded bitmap or video frame), as render target
  /// (<see cref="Usage.RenderTarget"/>) or as CPU-writable dynamic texture.
  /// </summary>
  /// <remarks>
  /// Pixel data is kept as raster bitmap until the first use for rendering, so textures can be created from any thread.
  /// The upload to the GPU happens lazily in the render thread.
  /// </remarks>
  public class Texture : IDisposable
  {
    /// <summary>
    /// Bilinear filtering, used for all textures (the default of Skia would be nearest neighbor, which leads to aliasing).
    /// </summary>
    protected static readonly SKSamplingOptions LinearSampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

    protected readonly object _syncObj = new object();
    protected readonly int _width;
    protected readonly int _height;
    protected readonly Format _format;
    protected readonly Usage _usage;
    protected SKBitmap _bitmap; // CPU data, for non-render-target textures
    protected SKSurface _renderSurface; // GPU data, for render targets
    protected SKImage _image; // Cached image to draw (GPU or raster)
    protected Surface _surface0;
    protected bool _isExternalImage;
    protected bool _disposed;

    /// <summary>
    /// Creates an empty texture.
    /// </summary>
    public Texture(int width, int height, Usage usage, Format format)
    {
      _width = width;
      _height = height;
      _usage = usage;
      _format = format;
      if ((usage & Usage.RenderTarget) != 0)
      {
        _renderSurface = GraphicsDevice.CreateGpuSurface(new SKImageInfo(width, height, format.ToColorType(), SKAlphaType.Premul));
        _renderSurface.Canvas.Clear(SKColors.Transparent);
      }
      else
        // CPU textures are filled with straight (not premultiplied) pixel data
        _bitmap = new SKBitmap(new SKImageInfo(width, height, format.ToColorType(), SKAlphaType.Unpremul));
    }

    protected Texture(SKImage image, Format format)
    {
      _width = image.Width;
      _height = image.Height;
      _format = format;
      _usage = Usage.None;
      _image = image;
      _isExternalImage = true;
    }

    /// <summary>
    /// Creates a texture from an image. The texture takes ownership of the <paramref name="image"/>.
    /// </summary>
    public static Texture FromImage(SKImage image)
    {
      return new Texture(image, Format.A8R8G8B8);
    }

    /// <summary>
    /// Creates a texture which draws the given OpenGL texture (RGBA, 8 bit per channel, premultiplied alpha, first row is the top row)
    /// without copying it. Has to be called from the render thread. The caller keeps the ownership of the OpenGL texture and must
    /// not delete it before the returned texture is disposed.
    /// </summary>
    public static Texture FromOpenGlTexture(uint textureId, int width, int height)
    {
      const uint GL_TEXTURE_2D = 0x0DE1;
      const uint GL_RGBA8 = 0x8058;
      GRContext context = GraphicsDevice.GrContext;
      if (context == null)
        return null;
      using (GRBackendTexture backendTexture = new GRBackendTexture(width, height, false, new GRGlTextureInfo(GL_TEXTURE_2D, textureId, GL_RGBA8)))
      {
        SKImage image = SKImage.FromTexture(context, backendTexture, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
        return image == null ? null : new Texture(image, Format.A8R8G8B8);
      }
    }

    /// <summary>
    /// Creates a texture from the pixels of a bitmap. The texture takes ownership of the <paramref name="bitmap"/>.
    /// </summary>
    public static Texture FromBitmap(SKBitmap bitmap)
    {
      Texture result = new Texture(SKImage.FromBitmap(bitmap), Format.A8R8G8B8);
      result._bitmap = bitmap;
      return result;
    }

    public int Width
    {
      get { return _width; }
    }

    public int Height
    {
      get { return _height; }
    }

    public Format Format
    {
      get { return _format; }
    }

    public bool IsRenderTarget
    {
      get { return _renderSurface != null; }
    }

    public bool Disposed
    {
      get { return _disposed; }
    }

    public SurfaceDescription GetLevelDescription(int level)
    {
      return new SurfaceDescription { Width = _width, Height = _height, Format = _format };
    }

    /// <summary>
    /// Gets the render target of this texture. Only available for textures created with <see cref="Usage.RenderTarget"/>.
    /// The returned surface must not be disposed.
    /// </summary>
    public Surface GetSurfaceLevel(int level)
    {
      lock (_syncObj)
      {
        if (_renderSurface == null)
          throw new InvalidOperationException("Texture is not a render target");
        return _surface0 ?? (_surface0 = new Surface(_renderSurface, _width, _height, false, this));
      }
    }

    /// <summary>
    /// Gets the image to be used when drawing this texture. For render target textures this is a snapshot of the current content.
    /// </summary>
    public SKImage Image
    {
      get
      {
        lock (_syncObj)
        {
          if (_disposed)
            return null;
          if (_image == null)
          {
            if (_renderSurface != null)
            {
              _renderSurface.Canvas.Flush();
              _image = _renderSurface.Snapshot();
            }
            else if (_bitmap != null)
            {
              using (SKImage raster = SKImage.FromBitmap(_bitmap))
                _image = GraphicsDevice.UploadImage(raster) ?? SKImage.FromBitmap(_bitmap);
            }
          }
          return _image;
        }
      }
    }

    /// <summary>
    /// Must be called when the content of the texture was changed (render target was drawn to or pixels were modified).
    /// </summary>
    public void InvalidateImage()
    {
      lock (_syncObj)
      {
        if (_isExternalImage && _bitmap == null)
          return;
        if (_image != null)
        {
          _image.Dispose();
          _image = null;
        }
      }
    }

    /// <summary>
    /// Gets access to the pixels of a CPU texture (not a render target). After modification <see cref="UnlockPixels"/> has to be called.
    /// </summary>
    public SKPixmap LockPixels()
    {
      lock (_syncObj)
      {
        if (_bitmap == null)
          throw new InvalidOperationException("Texture pixels are not accessible");
        return _bitmap.PeekPixels();
      }
    }

    public void UnlockPixels()
    {
      InvalidateImage();
    }

    /// <summary>
    /// Creates a shader sampling this texture with normalized (0..1) texture coordinates.
    /// </summary>
    public SKShader CreateShader(TextureAddress addressU, TextureAddress addressV)
    {
      SKImage image = Image;
      if (image == null)
        return null;
      return image.ToShader(addressU.ToTileMode(), addressV.ToTileMode(), LinearSampling, SKMatrix.CreateScale(1f / _width, 1f / _height));
    }

    /// <summary>
    /// Creates a shader sampling this texture with coordinates in pixels.
    /// </summary>
    public SKShader CreateShaderInPixels(TextureAddress addressU, TextureAddress addressV)
    {
      SKImage image = Image;
      if (image == null)
        return null;
      return image.ToShader(addressU.ToTileMode(), addressV.ToTileMode(), LinearSampling);
    }

    public virtual void Dispose()
    {
      lock (_syncObj)
      {
        if (_disposed)
          return;
        _disposed = true;
        if (_image != null)
          _image.Dispose();
        _image = null;
        if (_surface0 != null)
          _surface0.Dispose();
        _surface0 = null;
        if (_renderSurface != null)
          _renderSurface.Dispose();
        _renderSurface = null;
        if (_bitmap != null)
          _bitmap.Dispose();
        _bitmap = null;
      }
    }
  }
}
