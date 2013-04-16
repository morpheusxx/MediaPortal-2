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
using MediaPortal.Common.General;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.Rendering;
using MediaPortal.Utilities.DeepCopy;
using SlimDX.Direct3D9;

namespace MediaPortal.UI.SkinEngine.Controls.ImageSources
{
  /// <summary>
  /// Like <see cref="BitmapImageSource"/>, <see cref="MultiImageSource"/> acts as a source for the <see cref="Visuals.Image"/> control
  /// to access to conventional image formats. The primary difference between these two classes is that
  /// <see cref="MultiImageSource"/> is optimised for asynchronous image loading and frequent image changes,
  /// such as in a slide-show, and allows animated transitions between images.
  /// </summary>
  public class MultiImageSource : MultiImageSourceBase
  {
    protected AbstractProperty _imageSourceProperty;
    protected AbstractProperty _rotationProperty;
    protected AbstractProperty _decodePixelWidthProperty;
    protected AbstractProperty _decodePixelHeightProperty;
    protected AbstractProperty _thumbnailDimensionProperty;
    protected AbstractProperty _waitProperty;
    protected bool _thumbnail = false;

    protected TextureAsset _lastTexture = null;
    protected TextureAsset _nextTexture = null;
    protected TextureImageSource _imageSource;

    protected bool _source = true;

    #region Ctor

    public MultiImageSource()
    {
      Init();
      Attach();
    }

    void Init()
    {
      _imageSourceProperty = new SProperty(typeof(object), null);
      _rotationProperty = new SProperty(typeof(RightAngledRotation), RightAngledRotation.Zero);
      _decodePixelWidthProperty = new SProperty(typeof(int), 0);
      _decodePixelHeightProperty = new SProperty(typeof(int), 0);
      _thumbnailDimensionProperty = new SProperty(typeof(int), 0);
      _waitProperty = new SProperty(typeof(bool), true);
    }

    void Attach()
    {
      TryAttach(ImageSource);
      _imageSourceProperty.Attach(OnSourceChanged);
    }

    void Detach()
    {
      TryDetach(ImageSource);
      _imageSourceProperty.Detach(OnSourceChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      base.DeepCopy(source, copyManager);
      Detach();
      MultiImageSource mis = (MultiImageSource) source;
      ImageSource = copyManager.GetCopy(mis.ImageSource);
      DecodePixelWidth = mis.DecodePixelWidth;
      DecodePixelHeight = mis.DecodePixelHeight;
      Thumbnail = mis.Thumbnail;
      ThumbnailDimension = mis.ThumbnailDimension;
      Wait = mis.Wait;
      Attach();
      FreeData();
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Gets or sets the image for this source. When this property is changed over time, the last image is
    /// merged into the next image via one of the configured transitions in <see cref="MultiImageSourceBase.Transition"/>.
    /// </summary>
    public object ImageSource
    {
      get { return _imageSourceProperty.GetValue(); }
      set { _imageSourceProperty.SetValue(value); }
    }

    public AbstractProperty ImageSourceProperty
    {
      get { return _imageSourceProperty; }
    }

    /// <summary>
    /// Gets or sets an indicator if the transition effect needs to be finished before changing image source to next one.
    /// </summary>
    public bool Wait
    {
      get { return (bool) _waitProperty.GetValue(); }
      set { _waitProperty.SetValue(value); }
    }

    public AbstractProperty WaitProperty
    {
      get { return _waitProperty; }
    }

    public RightAngledRotation Rotation
    {
      get { return (RightAngledRotation) _rotationProperty.GetValue(); }
      set { _rotationProperty.SetValue(value); }
    }

    public AbstractProperty RotationProperty
    {
      get { return _rotationProperty; }
    }

    /// <summary>
    /// Gets or sets a value that determines the width of the image stored in memory. 
    /// For large images this can decrease memory use and improve performance.
    /// To preserve the image's aspect ratio only set one of DecodeWidth or DecodeHeight.
    /// </summary>
    public int DecodePixelWidth
    {
      get { return (int) _decodePixelWidthProperty.GetValue(); }
      set { _decodePixelWidthProperty.SetValue(value); }
    }

    public AbstractProperty DecodePixelWidthProperty
    {
      get { return _decodePixelWidthProperty; }
    }

    /// <summary>
    /// Gets or sets a value that determines the width of the image stored in memory. 
    /// For large images this can decrease memory use and improve performance.
    /// To preserve the image's aspect ratio only set one of DecodeWidth or DecodeHeight.
    /// </summary>
    public int DecodePixelHeight
    {
      get { return (int) _decodePixelHeightProperty.GetValue(); }
      set { _decodePixelHeightProperty.SetValue(value); }
    }

    public AbstractProperty DecodePixelHeightProperty
    {
      get { return _decodePixelHeightProperty; }
    }

    /// <summary>
    /// Gets or sets a value indicating that the image will be loaded as a thumbnail.
    /// </summary>
    /// <remarks>
    /// This is not an MPF accessible property. To set it use the Thumbnail property on the owner Image control.
    /// </remarks>
    public bool Thumbnail
    {
      get { return _thumbnail; }
      set { 
        if (value != _thumbnail) 
          FreeData(); 
        _thumbnail = value; 
      }
    }

    /// <summary>
    /// Gets or sets a value that determines dimension of a thumbnail.
    /// This property is only used in combination with <see cref="Thumbnail"/>=true, to force a specific dimension
    /// for thumnbnails (Windows thumbnail cache offers 32, 96, 256 and 1024 size, the minimum matching size is used).
    /// </summary>
    public int ThumbnailDimension
    {
      get { return (int) _thumbnailDimensionProperty.GetValue(); }
      set { _thumbnailDimensionProperty.SetValue(value); }
    }

    public AbstractProperty ThumbnailDimensionProperty
    {
      get { return _thumbnailDimensionProperty; }
    }

    #endregion

    #region ImageSource implementation

    public override void Allocate()
    {
      if (_source)
      {
        _source = false;
        _imageSource = ImageSource as TextureImageSource;
        string uri = ImageSource as string;
        if (_imageSource == null)
        {
          _nextTexture = null;
          if (_texture != null)
            CycleTextures(RightAngledRotation.Zero);
          if (string.IsNullOrEmpty(uri))
          {
            if (_texture != null)
              CycleTextures(RightAngledRotation.Zero);
          }
          else
          {
            _nextTexture = ContentManager.Instance.GetTexture(uri, DecodePixelWidth, DecodePixelHeight, Thumbnail);
            _nextTexture.ThumbnailDimension = ThumbnailDimension;
          }
        }
      }
      // Image source allocation can be done asynchronously, so check when the TextureAsset is created.
      if (_imageSource != null && !_imageSource.IsAllocated)
      {
        _imageSource.Allocate();
        _nextTexture = _imageSource.TextureAsset;
      }

      // Check our previous texture is allocated. Synchronous.
      if (_lastTexture != null && !_lastTexture.IsAllocated)
        _lastTexture.Allocate();
      // Check our current texture is allocated. Synchronous.
      if (_texture != null && !_texture.IsAllocated)
        _texture.Allocate();
      // Check our next texture is allocated. Asynchronous.
      if (_nextTexture != null)
      {
        if (!_nextTexture.LoadFailed)
          _nextTexture.AllocateAsync();
        if ((!_transitionActive || !Wait) && _nextTexture.IsAllocated)
          CycleTextures(Rotation);
      }
    }

    #endregion

    #region Protected methods

    protected override Texture LastTexture
    {
      get { return _lastTexture == null ? null : _lastTexture.Texture; }
    }

    protected override SizeF LastRawSourceSize
    {
      get { return _lastTexture == null ? SizeF.Empty : new SizeF(_lastTexture.Width, _lastTexture.Height); }
    }

    protected override RectangleF LastTextureClip
    {
      get { return _lastTexture == null ? RectangleF.Empty : new RectangleF(0, 0, _lastTexture.MaxU, _lastTexture.MaxV); }
    }

    protected override Texture CurrentTexture
    {
      get { return _texture == null ? null : _texture.Texture; }
    }

    protected override SizeF CurrentRawSourceSize
    {
      get { return _texture == null ? SizeF.Empty : new SizeF(_texture.Width, _texture.Height); }
    }

    protected override RectangleF CurrentTextureClip
    {
      get { return _texture == null ? RectangleF.Empty : new RectangleF(0, 0, _texture.MaxU, _texture.MaxV); }
    }

    public override bool IsAllocated
    {
      get { return _texture != null && _texture.IsAllocated; }
    }

    protected void CycleTextures(RightAngledRotation rotation)
    {
      // Current -> Last
      _lastTexture = _texture;
      _lastImageContext = _imageContext;
      // Next -> Current
      _texture = _nextTexture;
      _imageContext = new ImageContext
        {
            FrameSize = _frameSize,
            ShaderEffect = Effect,
            Rotation = rotation
        };
      // Clear next
      _nextTexture = null;

      if (_lastTexture != _texture)
      {
        StartTransition();
        FireChanged();
      }
    }

    protected void OnSourceChanged(AbstractProperty prop, object oldValue)
    {
      TryDetach(oldValue);
      TryAttach(ImageSource);
      _source = true;
    }

    protected override void OnImageSourceChanged(IObservable observable)
    {
      base.OnImageSourceChanged(observable);
      _source = true;
      FireChanged();
    }

    protected override void FreeData()
    {
      base.FreeData();
      IDisposable disposable = ImageSource as IDisposable;
      if (disposable != null)
        disposable.Dispose();
      _lastTexture = null;
      _texture = null;
      _nextTexture = null;
      _lastImageContext.Clear();
    }

    #endregion
  }
}
