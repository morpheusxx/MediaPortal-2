#region Copyright (C) 2007-2013 Team MediaPortal

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
using System.Drawing;
using MediaPortal.Common;
using MediaPortal.Common.General;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Presentation.Geometries;
using MediaPortal.UI.SkinEngine.Controls.Transforms;
using MediaPortal.UI.SkinEngine.Controls.Visuals;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.Rendering;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SharpDX.Direct3D9;
using SharpDX;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.Utilities.DeepCopy;
using Color = SharpDX.Color;
using Rectangle = SharpDX.Rectangle;
using RectangleF = SharpDX.RectangleF;

namespace MediaPortal.UI.SkinEngine.Controls.Brushes
{
  /// <summary>
  /// Brush which paints the video image of a player of type <see cref="ISlimDXVideoPlayer"/> provided by the <see cref="IPlayerManager"/>.
  /// </summary>
  public class VideoBrush : Brush
  {
    #region Consts

    protected const string EFFECT_BASE_VIDEO = "video_base";

    protected const string PARAM_TRANSFORM = "g_transform";
    protected const string PARAM_RELATIVE_TRANSFORM = "g_relativetransform";

    #endregion

    #region Protected fields

    protected AbstractProperty _streamProperty;
    protected AbstractProperty _geometryProperty;
    protected AbstractProperty _borderColorProperty;

    protected IGeometry _currentGeometry;
    protected Matrix _inverseRelativeTransformCache;
    protected ImageContext _imageContext;
    protected SizeF _scaledVideoSize;
    protected RectangleF _videoTextureClip;

    protected IGeometry _lastGeometry;
    protected string _lastEffect;
    protected Rectangle _lastCropVideoRect;
    protected Size _lastVideoSize;
    protected SizeF _lastAspectRatio;
    protected int _lastDeviceWidth;
    protected int _lastDeviceHeight;
    protected Vector4 _lastFrameData;
    protected RectangleF _lastVertsBounds;
    protected Texture _texture = null;
    protected volatile bool _refresh = true;

    #endregion

    #region Ctor

    public VideoBrush()
    {
      Init();
      Attach();
    }

    void Init()
    {
      _streamProperty = new SProperty(typeof(int), 0);
      _geometryProperty = new SProperty(typeof(string), null);
      _borderColorProperty = new SProperty(typeof(Color), Color.Black);

      _imageContext = new ImageContext
        {
          OnRefresh = OnImagecontextRefresh,
          ExtraParameters = new System.Collections.Generic.Dictionary<string, object>()
        };
    }

    void Attach()
    {
      _geometryProperty.Attach(OnGeometryChange);
    }

    void Detach()
    {
      _geometryProperty.Detach(OnGeometryChange);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      VideoBrush b = (VideoBrush) source;
      Stream = b.Stream;
      Geometry = b.Geometry;
      BorderColor = b.BorderColor;
      _refresh = true;
      Attach();
    }

    public override void Dispose()
    {
      base.Dispose();
      TryDispose(ref _texture);
    }

    #endregion

    #region Protected & private members

    void OnGeometryChange(AbstractProperty prop, object oldVal)
    {
      string geometryName = Geometry;
      if (String.IsNullOrEmpty(geometryName))
      {
        _currentGeometry = null;
        return;
      }
      IGeometryManager geometryManager = ServiceRegistration.Get<IGeometryManager>();
      IGeometry geometry;
      if (geometryManager.AvailableGeometries.TryGetValue(geometryName, out geometry))
        _currentGeometry = geometry;
      else
      {
        ServiceRegistration.Get<ILogger>().Debug("VideoBrush: Geometry '{0}' does not exist", geometryName);
        _currentGeometry = null;
      }
    }

    protected override void OnRelativeTransformChanged(IObservable trans)
    {
      _refresh = true;
      base.OnRelativeTransformChanged(trans);
    }

    protected IGeometry ChooseVideoGeometry(IVideoPlayer player)
    {
      if (_currentGeometry != null)
        return _currentGeometry;
      if (player.GeometryOverride != null)
        return player.GeometryOverride;

      return ServiceRegistration.Get<IGeometryManager>().DefaultVideoGeometry;
    }

    protected bool RefreshEffectParameters(IVideoPlayer player)
    {
      ISlimDXVideoPlayer sdvPlayer = player as ISlimDXVideoPlayer;
      if (sdvPlayer == null)
        return false;
      SizeF aspectRatio = sdvPlayer.VideoAspectRatio;
      Size playerSize = sdvPlayer.VideoSize;
      Rectangle cropVideoRect = sdvPlayer.CropVideoRect;
      IGeometry geometry = ChooseVideoGeometry(player);
      string effectName = player.EffectOverride;
      int deviceWidth = GraphicsDevice.Width; // To avoid threading issues if the device size changes
      int deviceHeight = GraphicsDevice.Height;
      RectangleF vertsBounds = _vertsBounds;

      // Do we need a refresh?
      if (!_refresh &&
          _lastVideoSize == playerSize &&
          _lastAspectRatio == aspectRatio &&
          _lastCropVideoRect == cropVideoRect &&
          _lastGeometry == geometry &&
          _lastEffect == effectName &&
          _lastDeviceWidth == deviceWidth &&
          _lastDeviceHeight == deviceHeight &&
          _lastVertsBounds == vertsBounds)
        return true;

      SizeF targetSize = vertsBounds.SizeF();

      lock (sdvPlayer.SurfaceLock)
      {
        Surface surface = sdvPlayer.Surface;
        if (surface == null)
        {
          _refresh = true;
          return false;
        }
        SurfaceDescription desc = surface.Description;
        _videoTextureClip = SharpDXHelper.CreateRectangleF(cropVideoRect.X / desc.Width, cropVideoRect.Y / desc.Height,
            cropVideoRect.Width / desc.Width, cropVideoRect.Height / desc.Height);
      }
      _scaledVideoSize = cropVideoRect.Size();

      // Correct aspect ratio for anamorphic video
      if (!aspectRatio.IsEmpty && geometry.RequiresCorrectAspectRatio)
      {
        float pixelRatio = aspectRatio.Width / aspectRatio.Height;
        _scaledVideoSize.Width = _scaledVideoSize.Height * pixelRatio;
      }
      // Adjust target size to match final Skin scaling
      targetSize = ImageContext.AdjustForSkinAR(targetSize);

      // Adjust video size to fit desired geometry
      _scaledVideoSize = geometry.Transform(_scaledVideoSize, targetSize);

      // Cache inverse RelativeTransform
      Transform relativeTransform = RelativeTransform;
      _inverseRelativeTransformCache = (relativeTransform == null) ? Matrix.Identity : Matrix.Invert(relativeTransform.GetTransform());

      // Prepare our ImageContext
      _imageContext.FrameSize = targetSize;
      _imageContext.ShaderBase = EFFECT_BASE_VIDEO;
      _imageContext.ShaderTransform = geometry.Shader;
      _imageContext.ShaderEffect = player.EffectOverride;

      // Store state
      _lastFrameData = new Vector4(playerSize.Width, playerSize.Height, 0.0f, 0.0f);
      _lastVideoSize = playerSize;
      _lastAspectRatio = aspectRatio;
      _lastGeometry = geometry;
      _lastCropVideoRect = cropVideoRect;
      _lastEffect = effectName;
      _lastDeviceWidth = deviceWidth;
      _lastDeviceHeight = deviceHeight;

      _refresh = false;
      return true;
    }

    protected void OnImagecontextRefresh()
    {
      _imageContext.ExtraParameters[PARAM_RELATIVE_TRANSFORM] = _inverseRelativeTransformCache;
      _imageContext.ExtraParameters[PARAM_TRANSFORM] = GetCachedFinalBrushTransform();
    }

    #endregion

    #region Public properties

    public AbstractProperty StreamProperty
    {
      get { return _streamProperty; }
    }

    /// <summary>
    /// Gets or sets the number of the player stream to be shown.
    /// </summary>
    public int Stream
    {
      get { return (int) _streamProperty.GetValue(); }
      set { _streamProperty.SetValue(value); }
    }

    public AbstractProperty GeometryProperty
    {
      get { return _geometryProperty; }
    }

    /// <summary>
    /// Allows the skin to override the video gemoetry asked for by the player.
    /// </summary>
    public string Geometry
    {
      get { return (string) _geometryProperty.GetValue(); }
      set { _geometryProperty.SetValue(value); }
    }

    public AbstractProperty BorderColorProperty
    {
      get { return _borderColorProperty; }
    }

    /// <summary>
    /// Gets or sets the color to be used for drawing bars/borders around the video
    /// </summary>
    public Color BorderColor
    {
      get { return (Color) _borderColorProperty.GetValue(); }
      set { _borderColorProperty.SetValue(value); }
    }

    #endregion

    #region Public members

    public override void SetupBrush(FrameworkElement parent, ref PositionColoredTextured[] verts, float zOrder, bool adaptVertsToBrushTexture)
    {
      base.SetupBrush(parent, ref verts, zOrder, adaptVertsToBrushTexture);
      if (ServiceRegistration.Get<IPlayerManager>(false) == null)
        ServiceRegistration.Get<ILogger>().Debug("VideoBrush.SetupBrush: Player manager not found");
    }

    protected override bool BeginRenderBrushOverride(PrimitiveBuffer primitiveContext, RenderContext renderContext)
    {
      ISlimDXVideoPlayer player;
      if (!GetPlayer(out player))
        return false;

      if (!RefreshEffectParameters(player))
        return false;

      lock (player.SurfaceLock)
      {
        Surface playerSurface = player.Surface;
        if (playerSurface == null)
          return false;
        DeviceEx device = SkinContext.Device;
        SurfaceDescription desc = playerSurface.Description;
        SurfaceDescription? textureDesc = _texture == null ? new SurfaceDescription?() : _texture.GetLevelDescription(0);
        if (!textureDesc.HasValue || textureDesc.Value.Width != desc.Width || textureDesc.Value.Height != desc.Height)
        {
          TryDispose(ref _texture);
          _texture = new Texture(device, desc.Width, desc.Height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
        }
        Surface target = _texture.GetSurfaceLevel(0);
        device.StretchRectangle(playerSurface, target, TextureFilter.None);
      }
      
      // Handling of multipass (3D) rendering, transformed rect contains the clipped area of the source image (i.e. left side in Side-By-Side mode).
      RectangleF tranformedRect;
      GraphicsDevice.RenderPipeline.GetVideoClip(_videoTextureClip, out tranformedRect);
      return _imageContext.StartRender(renderContext, _scaledVideoSize, _texture, tranformedRect, BorderColor.ToBgra(), _lastFrameData);
    }

    protected virtual bool GetPlayer(out ISlimDXVideoPlayer player)
    {
      player = null;
      IPlayerContextManager playerContextManager = ServiceRegistration.Get<IPlayerContextManager>(false);
      if (playerContextManager == null)
        return false;

      player = playerContextManager[Stream] as ISlimDXVideoPlayer;
      return player != null;
    }

    public override void EndRender()
    {
      _imageContext.EndRender();
    }

    #endregion
  }
}
