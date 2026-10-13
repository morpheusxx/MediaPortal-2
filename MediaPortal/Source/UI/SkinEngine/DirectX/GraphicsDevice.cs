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
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.DirectX.RenderPipelines;
using MediaPortal.UI.SkinEngine.DirectX.RenderStrategy;
using MediaPortal.UI.SkinEngine.Gpu;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using MediaPortal.UI.SkinEngine.Utils;
using MediaPortal.UI.SkinEngine.Utils;
using SharpDX;
using SkiaSharp;
using Matrix = SharpDX.Matrix;
using Rectangle = SharpDX.Rectangle;

namespace MediaPortal.UI.SkinEngine.DirectX
{
  public delegate void WorkDlgt();

  public enum RenderPassType
  {
    SingleOrFirstPass = 0,
    SecondPass
  }

  /// <summary>
  /// How incoming fragments are combined with the destination.
  /// </summary>
  public enum BlendState
  {
    /// <summary>Premultiplied alpha: <c>dst = src + dst * (1 - srcAlpha)</c>.</summary>
    Default,
    /// <summary>Only the alpha channel of the incoming fragments is used to scale the destination (opacity masks).</summary>
    AlphaChannelOnly,
    /// <summary>The destination is replaced.</summary>
    Replace
  }

  /// <summary>
  /// The graphics device of the SkinEngine. It owns the Skia GPU context (the OpenGL context is provided by the window host),
  /// the current render target and the state which was set by the effects before primitives are drawn.
  /// </summary>
  internal static class GraphicsDevice
  {
    #region Variables

    private static readonly RenderSetup _setup = new RenderSetup();
    private static readonly ReaderWriterLockSlim _renderAndResourceAccessLock = new ReaderWriterLockSlim();
    private static IRenderHost _host;
    private static GRContext _grContext;
    private static GRBackendRenderTarget _backBufferTarget;
    private static SKSurface _backBufferSurface;
    private static Surface _backBuffer;
    private static int _backBufferWidth;
    private static int _backBufferHeight;
    private static Surface _currentTarget;
    private static bool _deviceOk = true;
    private static ScreenManager _screenManager = null;

    // State which is set by the effects before a primitive buffer is rendered
    private static SKShader _currentShader;
    private static SKMatrix _currentMatrix = SKMatrix.CreateIdentity();
    private static bool _currentUsesVertexColors;
    private static BlendState _blendState = BlendState.Default;

    #endregion

    public static Matrix TransformWorld;
    public static Matrix TransformView;
    public static Matrix TransformProjection;
    public static Matrix FinalTransform = Matrix.Identity;

    // Render process related events
    public static event EventHandler DeviceSceneBegin;
    public static event EventHandler DeviceSceneEnd;
    public static event EventHandler DeviceScenePresented;

    // RenderModeType related fields
    private static int _currentRenderStrategyIndex = 0;
    private static List<IRenderStrategy> _renderStrategies;

    // RenderPipeline related fields
    private static int _currentRenderPipeplineIndex;
    private static List<IRenderPipeline> _renderPipelines;

    /// <summary>
    /// Returns the information if the graphics device is healthy, which means it was neither lost nor removed.
    /// </summary>
    public static bool DeviceOk
    {
      get { return _deviceOk; }
    }

    /// <summary>
    /// Returns the target rendering target framerate. This value can be changed according to screen refresh rate or video fps using
    /// <see cref="SetFrameRate"/>.
    /// </summary>
    public static double TargetFrameRate
    {
      get { return RenderStrategy.TargetFrameRate; }
    }

    /// <summary>
    /// Returns the desired time per frame in ms.
    /// </summary>
    public static double MsPerFrame
    {
      get { return RenderStrategy.MsPerFrame; }
    }

    public static ScreenManager ScreenManager
    {
      get { return _screenManager; }
      internal set { _screenManager = value; }
    }

    /// <summary>
    /// Gets the back-buffer width.
    /// </summary>
    public static int Width
    {
      get { return _backBufferWidth; }
    }

    /// <summary>
    /// Gets the back-buffer height.
    /// </summary>
    public static int Height
    {
      get { return _backBufferHeight; }
    }

    public static RenderSetup Setup
    {
      get { return _setup; }
    }

    public static GRContext GrContext
    {
      get { return _grContext; }
    }

    /// <summary>
    /// Gets the display mode of the monitor the window is shown on.
    /// </summary>
    public static DisplayMode CurrentDisplayMode
    {
      get
      {
        return new DisplayMode
          {
            Width = _setup.DesktopWidth,
            Height = _setup.DesktopHeight,
            RefreshRate = _host == null ? 60 : (int) Math.Round(_host.RefreshRate),
            Format = Format.A8R8G8B8
          };
      }
    }

    public static RenderPassType RenderPass { get; set; }

    /// <summary>
    /// Lock to be used during GPU resource access and during rendering.
    /// </summary>
    public static ReaderWriterLockSlim RenderAndResourceAccessLock
    {
      get { return _renderAndResourceAccessLock; }
    }

    #region Resource creation

    /// <summary>
    /// Creates a GPU render surface. If no GPU context is available (headless), a raster surface is created.
    /// </summary>
    public static SKSurface CreateGpuSurface(SKImageInfo info)
    {
      GRContext context = _grContext;
      SKSurface result = context == null ? null : SKSurface.Create(context, false, info);
      return result ?? SKSurface.Create(info);
    }

    /// <summary>
    /// Uploads a raster image to the GPU. Returns <c>null</c>, if this isn't possible, i.e. when called outside of the render thread.
    /// </summary>
    public static SKImage UploadImage(SKImage raster)
    {
      GRContext context = _grContext;
      if (context == null || !_setup.IsRenderThread)
        return null;
      return raster.ToTextureImage(context);
    }

    #endregion

    #region Render target handling

    /// <summary>
    /// Gets the canvas of the current render target.
    /// </summary>
    public static SKCanvas Canvas
    {
      get { return _currentTarget == null ? null : _currentTarget.Canvas; }
    }

    public static Surface BackBuffer
    {
      get { return _backBuffer; }
    }

    public static Surface GetRenderTarget()
    {
      return _currentTarget;
    }

    public static void SetRenderTarget(Surface target)
    {
      _currentTarget = target;
    }

    public static void Clear(SKColor color)
    {
      SKCanvas canvas = Canvas;
      if (canvas != null)
        canvas.Clear(color);
    }

    /// <summary>
    /// Clears the given area of the current render target.
    /// </summary>
    public static void ClearRect(SharpDX.RectangleF area, SKColor color)
    {
      SKCanvas canvas = Canvas;
      if (canvas == null)
        return;
      canvas.Save();
      canvas.ResetMatrix();
      canvas.ClipRect(SKRect.Create((float) Math.Floor(area.Left), (float) Math.Floor(area.Top),
          (float) (Math.Ceiling(area.Right) - Math.Floor(area.Left)), (float) (Math.Ceiling(area.Bottom) - Math.Floor(area.Top))));
      canvas.Clear(color);
      canvas.Restore();
      RenderTargetChanged();
    }

    /// <summary>
    /// Copies the complete <paramref name="source"/> surface to the <paramref name="destination"/> surface.
    /// </summary>
    public static void StretchRectangle(Surface source, Surface destination)
    {
      StretchRectangle(source, new Rectangle(0, 0, source.Width, source.Height), destination, new Rectangle(0, 0, destination.Width, destination.Height), false);
    }

    /// <summary>
    /// Copies a part of the <paramref name="source"/> surface to the <paramref name="destination"/> surface.
    /// </summary>
    public static void StretchRectangle(Surface source, Rectangle sourceRect, Surface destination, Rectangle destinationRect, bool linear)
    {
      using (SKImage image = source.Snapshot())
      using (SKPaint paint = new SKPaint { FilterQuality = linear ? SKFilterQuality.Medium : SKFilterQuality.None, BlendMode = SKBlendMode.Src })
      {
        SKCanvas canvas = destination.Canvas;
        canvas.Save();
        canvas.ResetMatrix();
        canvas.DrawImage(image, SKRect.Create(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height),
            SKRect.Create(destinationRect.X, destinationRect.Y, destinationRect.Width, destinationRect.Height), paint);
        canvas.Restore();
      }
      destination.MarkChanged();
    }

    #endregion

    #region Draw state (set by the effects, consumed by the primitive buffers)

    /// <summary>
    /// Sets the shader and transformation which will be used for the primitive buffers which are rendered next.
    /// </summary>
    /// <param name="shader">Shader to use for the fill of the primitives.</param>
    /// <param name="transform">Transformation of the primitive vertices into back buffer coordinates.</param>
    /// <param name="usesVertexColors">Indicates that the vertex colors are to be modulated with the shader output.</param>
    public static void SetDrawState(SKShader shader, Matrix transform, bool usesVertexColors)
    {
      _currentShader = shader;
      _currentMatrix = ToSkMatrix(transform);
      _currentUsesVertexColors = usesVertexColors;
    }

    public static void ClearDrawState()
    {
      _currentShader = null;
      _currentUsesVertexColors = false;
    }

    public static SKShader CurrentShader
    {
      get { return _currentShader; }
    }

    public static SKMatrix CurrentMatrix
    {
      get { return _currentMatrix; }
    }

    public static bool CurrentUsesVertexColors
    {
      get { return _currentUsesVertexColors; }
    }

    /// <summary>
    /// Converts a row-vector 4x4 matrix as used by the SkinEngine into a 3x3 Skia matrix. The z coordinate is ignored.
    /// </summary>
    public static SKMatrix ToSkMatrix(Matrix m)
    {
      return new SKMatrix(m.M11, m.M21, m.M41, m.M12, m.M22, m.M42, m.M14, m.M24, m.M44);
    }

    #endregion

    #region Sampler and texture state

    private static readonly SamplerState[] _samplers =
      {
        SamplerState.Default, SamplerState.Default, SamplerState.Default, SamplerState.Default
      };
    private static readonly Texture[] _boundTextures = new Texture[4];

    public static SamplerState GetSamplerState(int sampler)
    {
      return _samplers[sampler];
    }

    public static void SetSamplerAddress(int sampler, TextureAddress addressU, TextureAddress addressV)
    {
      _samplers[sampler].AddressU = addressU;
      _samplers[sampler].AddressV = addressV;
    }

    public static void SetSamplerBorderColor(int sampler, int bgraColor)
    {
      _samplers[sampler].BorderColor = bgraColor;
    }

    /// <summary>
    /// Binds a texture to a sampler. It is used by effects which are started without an explicit texture.
    /// </summary>
    public static void SetTexture(int sampler, Texture texture)
    {
      _boundTextures[sampler] = texture;
    }

    public static Texture GetTexture(int sampler)
    {
      return _boundTextures[sampler];
    }

    /// <summary>
    /// Has to be called after the content of the current render target was changed, so a texture which uses this target
    /// refreshes its image.
    /// </summary>
    public static void RenderTargetChanged()
    {
      if (_currentTarget != null)
        _currentTarget.MarkChanged();
    }

    #endregion

    #region Blending

    public static BlendState BlendState
    {
      get { return _blendState; }
    }

    public static void SetBlendState(BlendState blendState)
    {
      _blendState = blendState;
    }

    public static SKBlendMode SkBlendMode
    {
      get
      {
        switch (_blendState)
        {
          case BlendState.AlphaChannelOnly:
            return SKBlendMode.DstIn;
          case BlendState.Replace:
            return SKBlendMode.Src;
          default:
            return SKBlendMode.SrcOver;
        }
      }
    }

    /// <summary>
    /// Sets the render states to enable the default alpha blend compositing.
    /// </summary>
    public static void EnableDefaultAlphaBlending()
    {
      _blendState = BlendState.Default;
    }

    /// <summary>
    /// Sets the render states to disable alpha compositing.
    /// </summary>
    public static void DisableAlphaBlending()
    {
      _blendState = BlendState.Replace;
    }

    /// <summary>
    /// Gets an indicator, if alpha blending for opacity masks is enabled. Enabling is done by <see cref="EnableAlphaChannelBlending"/>,
    /// disabling by <see cref="DisableAlphaChannelBlending"/>.
    /// </summary>
    public static bool IsAlphaChannelBlendingEnabled
    {
      get { return _blendState == BlendState.AlphaChannelOnly; }
    }

    /// <summary>
    /// Sets the render states to enable alpha blending for opacity masks, where only the alpha channel of incoming fragments is blended.
    /// </summary>
    public static void EnableAlphaChannelBlending()
    {
      _blendState = BlendState.AlphaChannelOnly;
    }

    /// <summary>
    /// Sets the render states to disable separate blending for the alpha channel.
    /// </summary>
    public static void DisableAlphaChannelBlending()
    {
      EnableDefaultAlphaBlending();
    }

    /// <summary>
    /// Alpha testing is not used any more; fragments with a zero alpha value don't change the destination anyway.
    /// </summary>
    public static void EnableAlphaTest()
    { }

    public static void DisableAlphaTest()
    { }

    #endregion

    #region Initialization

    public static void ExecuteInMainThread(WorkDlgt method)
    {
      try
      {
        _host.Invoke(() => method());
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("GraphicsDevice: Error executing action in MainThread.", ex);
      }
    }

    public static void ReCreateDXDevice()
    {
      _screenManager.ExecuteWithTempReleasedResources(() => ExecuteInMainThread(DoReCreateDevice_MainThread));
    }

    /// <summary>
    /// Initializes the graphics device with the given window host. The host must already have created the OpenGL context.
    /// </summary>
    /// <remarks>
    /// This method has to be called from the render thread, which is the thread which owns the OpenGL context.
    /// </remarks>
    internal static void Initialize_MainThread(IRenderHost host)
    {
      _host = host;
      _setup.Host = host;
      DoReCreateDevice_MainThread();
    }

    /// <summary>
    /// Creates or re-creates the Skia GPU context and the back buffer.
    /// </summary>
    internal static void DoReCreateDevice_MainThread()
    {
      try
      {
        ServiceRegistration.Get<ILogger>().Debug("GraphicsDevice: Initializing graphics device");

        UIResourcesHelper.ReleaseUIResources();
        ReleaseBackBuffer();
        if (_grContext != null)
          _grContext.Dispose();
        _grContext = _host.CreateGrContext();
        if (_grContext == null)
          throw new InvalidOperationException("Unable to create the Skia GPU context");

        SetupRenderStrategies();
        SetupRenderPipelines();
        CreateBackBuffer();
        AdaptTargetFrameRateToRefreshRate(_host.RefreshRate);
        ServiceRegistration.Get<ILogger>().Info("GraphicsDevice: Initialized {0}x{1} ({2} Hz), {3}", Width, Height, TargetFrameRate, _host.RendererDescription);
        SetRenderState();
        UIResourcesHelper.ReallocUIResources();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Critical("GraphicsDevice: Failed to setup the graphics device", ex);
        Environment.Exit(0);
      }
    }

    private static void ReleaseBackBuffer()
    {
      if (_backBuffer != null)
        _backBuffer.Dispose();
      _backBuffer = null;
      _currentTarget = null;
      if (_backBufferSurface != null)
        _backBufferSurface.Dispose();
      _backBufferSurface = null;
      if (_backBufferTarget != null)
        _backBufferTarget.Dispose();
      _backBufferTarget = null;
    }

    private static void CreateBackBuffer()
    {
      int width = _host.FramebufferWidth;
      int height = _host.FramebufferHeight;
      if (width <= 0 || height <= 0)
        return;
      ReleaseBackBuffer();
      _backBufferTarget = new GRBackendRenderTarget(width, height, _setup.MultiSampleCount, 8, new GRGlFramebufferInfo(0, 0x8058 /* GL_RGBA8 */));
      _backBufferSurface = SKSurface.Create(_grContext, _backBufferTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
      if (_backBufferSurface == null)
        throw new InvalidOperationException("Unable to create the back buffer surface");
      _backBufferWidth = width;
      _backBufferHeight = height;
      _backBuffer = new Surface(_backBufferSurface, width, height, false, null);
      _currentTarget = _backBuffer;
    }

    /// <summary>
    /// Has to be called by the window host when the size of the framebuffer was changed.
    /// </summary>
    internal static void OnFramebufferResized()
    {
      if (_grContext == null)
        return;
      if (_host.FramebufferWidth == _backBufferWidth && _host.FramebufferHeight == _backBufferHeight)
        return;
      _renderAndResourceAccessLock.EnterWriteLock();
      try
      {
        CreateBackBuffer();
        SetCameraProjection(Width, Height);
      }
      finally
      {
        _renderAndResourceAccessLock.ExitWriteLock();
      }
    }

    /// <summary>
    /// Setups all <see cref="IRenderStrategy"/>s.
    /// </summary>
    private static void SetupRenderStrategies()
    {
      _renderStrategies = new List<IRenderStrategy>
        {
          new Default(_setup),
          new VSync(_setup),
          new MaxPerformance(_setup)
        };
      _currentRenderStrategyIndex = 0;
      _host.VSync = RenderStrategy.PresentVSync;
    }

    /// <summary>
    /// Setups all <see cref="IRenderPipeline"/>s.
    /// </summary>
    private static void SetupRenderPipelines()
    {
      _renderPipelines = new List<IRenderPipeline>
        {
          new SinglePass2DRenderPipeline(),
          new SBSRenderPipeline(),
          new TABRenderPipeline(),
          new SBS2DRenderPipeline(),
          new TAB2DRenderPipeline(),
        };
      _currentRenderPipeplineIndex = 0;
    }

    /// <summary>
    /// Gets the current <see cref="IRenderStrategy"/>.
    /// </summary>
    public static IRenderStrategy RenderStrategy
    {
      get { return _renderStrategies[_currentRenderStrategyIndex]; }
    }

    /// <summary>
    /// Switches through all possible RenderStrategies.
    /// </summary>
    public static void NextRenderStrategy()
    {
      _currentRenderStrategyIndex = (_currentRenderStrategyIndex + 1) % _renderStrategies.Count;
      _host.VSync = RenderStrategy.PresentVSync;
      ServiceRegistration.Get<ILogger>().Info("GraphicsDevice: Render strategy {0}, {1} Hz", RenderStrategy.Name, TargetFrameRate);
    }

    /// <summary>
    /// Gets the current <see cref="IRenderPipeline"/>.
    /// </summary>
    public static IRenderPipeline RenderPipeline
    {
      get { return _renderPipelines[_currentRenderPipeplineIndex]; }
    }

    /// <summary>
    /// Switches through all possible RenderPipelines.
    /// </summary>
    public static void NextRenderPipeline()
    {
      _currentRenderPipeplineIndex = (_currentRenderPipeplineIndex + 1) % _renderPipelines.Count;
    }

    internal static void Dispose()
    {
      ReleaseBackBuffer();
      if (_grContext != null)
        _grContext.Dispose();
      _grContext = null;
      _renderAndResourceAccessLock.Dispose();
    }

    public static void SetFrameRate(double frameRate)
    {
      RenderStrategy.SetTargetFrameRate(frameRate);
    }

    private static void AdaptTargetFrameRateToRefreshRate(double refreshRate)
    {
      SetFrameRate(refreshRate);
    }

    /// <summary>
    /// Resets the graphics device. This will release all screens, other UI resources and our back buffer, and re-create them.
    /// </summary>
    public static bool Reset()
    {
      ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: Resetting device...");
      _screenManager.ExecuteWithTempReleasedResources(() => ExecuteInMainThread(() =>
        {
          UIResourcesHelper.ReleaseUIResources();

          if (ContentManager.Instance.TotalAllocationSize != 0)
            ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: ContentManager.TotalAllocationSize = {0}, should be 0!", ContentManager.Instance.TotalAllocationSize / (1024 * 1024));

          if (_grContext != null)
            _grContext.AbandonContext(false);
          DoReCreateDevice_MainThread();
        }));
      ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: Device successfully reset");
      return true;
    }

    /// <summary>
    /// Reclaims the device if it has been lost (see <see cref="DeviceOk"/>).
    /// </summary>
    /// <returns><c>true</c>, if the device could successfully be reclaimed. <c>false</c>, if the device could not be reclaimed.</returns>
    public static bool ReclaimDevice()
    {
      try
      {
        if (_grContext != null && !_grContext.IsAbandoned)
          _deviceOk = true;
        else
        {
          Reset();
          _deviceOk = _grContext != null && !_grContext.IsAbandoned;
        }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: Reclaiming device failed", ex);
        _deviceOk = false;
      }
      return !_deviceOk;
    }

    /// <summary>
    /// Sets the default render states and the projection.
    /// </summary>
    public static void SetRenderState()
    {
      EnableDefaultAlphaBlending();
      SetCameraProjection(Width, Height);
    }

    /// <summary>
    /// Creates the camera and projection matrices which are used in future rendering.
    /// </summary>
    /// <remarks>
    /// The primitives are drawn directly in back buffer pixel coordinates (origin top left), so this is the identity.
    /// </remarks>
    public static void SetCameraProjection(int width, int height)
    {
      TransformView = Matrix.Identity;
      TransformProjection = Matrix.Identity;
      FinalTransform = Matrix.Identity;
    }

    #endregion

    /// <summary>
    /// Fires an event if listeners are available.
    /// </summary>
    private static void Fire(EventHandler eventHandler)
    {
      try
      {
        if (eventHandler != null)
          eventHandler(null, EventArgs.Empty);
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Error("Error executing render event handler:", e);
      }
    }

    /// <summary>
    /// Renders the entire scene. Has to be called from the render callback of the window host; the host swaps the buffers afterwards.
    /// </summary>
    /// <param name="doWaitForNextFame"><c>true</c>, if this method should wait to the correct frame start time
    /// before it renders, else <c>false</c>.</param>
    /// <returns><c>true</c>, if the caller should wait some milliseconds before rendering the next time.</returns>
    public static bool Render(bool doWaitForNextFame)
    {
      if (_grContext == null || _backBuffer == null || !_deviceOk)
        return true;

      IRenderStrategy renderStrategy = RenderStrategy;
      IRenderPipeline pipeline = RenderPipeline;

      renderStrategy.BeginRender(doWaitForNextFame);

      _renderAndResourceAccessLock.EnterReadLock();

      bool doReset = false;
      try
      {
        _grContext.ResetContext(); // The video player or other GL users might have changed the GL state
        Fire(DeviceSceneBegin);

        pipeline.BeginRender();

        pipeline.Render();

        pipeline.EndRender();

        Fire(DeviceSceneEnd);

        _backBufferSurface.Canvas.Flush();
        _grContext.Flush();

        Fire(DeviceScenePresented);

        ContentManager.Instance.Clean();
      }
      catch (Exception e)
      {
        doReset = true;
        _deviceOk = _grContext != null && !_grContext.IsAbandoned;
        ServiceRegistration.Get<ILogger>().Warn("GraphicsDevice: Exception during rendering, device ok: {0}", e, _deviceOk);
        return !_deviceOk;
      }
      finally
      {
        _renderAndResourceAccessLock.ExitReadLock();
        // If there are exceptions during render pass, the device can stay in an invalid state.
        // So we prefer to reset the device and try continue rendering.
        if (doReset)
          Reset();
      }
      return false;
    }
  }
}
