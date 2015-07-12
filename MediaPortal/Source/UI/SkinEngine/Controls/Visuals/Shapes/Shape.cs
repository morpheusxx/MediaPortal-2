#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using MediaPortal.Common.General;
using MediaPortal.UI.SkinEngine.DirectX11;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.Rendering;
using SharpDX;
using SharpDX.Direct2D1;
using Brush = MediaPortal.UI.SkinEngine.Controls.Brushes.Brush;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.UI.SkinEngine.Controls.Visuals.Shapes
{
  /// <summary>
  /// Describes the shape that joins two lines or segments.
  /// </summary>
  public enum PenLineJoin
  {
    /// <summary>
    /// Line joins use regular angular vertices. This is the default behavior in MPF.
    /// </summary>
    Miter,
    /// <summary>
    /// Line joins use beveled vertices.
    /// </summary>
    Bevel,
    /// <summary>
    /// Line joins use rounded vertices. This is currently not supported and will be rendered using the default behavior.
    /// </summary>
    Round
  }

  public class Shape : FrameworkElement
  {
    #region Protected fields

    protected AbstractProperty _stretchProperty;
    protected AbstractProperty _fillProperty;
    protected AbstractProperty _strokeProperty;
    protected AbstractProperty _strokeThicknessProperty;
    protected AbstractProperty _strokeLineJoinProperty;

    protected volatile bool _performLayout;

    protected bool _fillDisabled;
    protected SharpDX.Direct2D1.Geometry _geometry;
    protected readonly object _resourceRenderLock = new object();

    #endregion

    #region Ctor

    public Shape()
    {
      Init();
      Attach();
    }

    public override void Dispose()
    {
      lock (_resourceRenderLock)
      {
        Detach();
        MPF.TryCleanupAndDispose(Fill);
        MPF.TryCleanupAndDispose(Stroke);
        lock (_resourceRenderLock)
          TryDispose(ref _geometry);
        base.Dispose();
      }
    }

    void Init()
    {
      _fillProperty = new SProperty(typeof(Brush), null);
      _strokeProperty = new SProperty(typeof(Brush), null);
      _strokeThicknessProperty = new SProperty(typeof(double), 1.0);
      _strokeLineJoinProperty = new SProperty(typeof(PenLineJoin), PenLineJoin.Miter);
      _stretchProperty = new SProperty(typeof(Stretch), Stretch.None);
    }

    void Attach()
    {
      _fillProperty.Attach(OnFillBrushPropertyChanged);
      _strokeProperty.Attach(OnStrokeBrushPropertyChanged);
      _strokeThicknessProperty.Attach(OnStrokeThicknessChanged);
      _strokeLineJoinProperty.Attach(OnStrokeLineJoinChanged);
    }

    void Detach()
    {
      _fillProperty.Detach(OnFillBrushPropertyChanged);
      _strokeProperty.Detach(OnStrokeBrushPropertyChanged);
      _strokeThicknessProperty.Detach(OnStrokeThicknessChanged);
      _strokeLineJoinProperty.Detach(OnStrokeLineJoinChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      Shape s = (Shape)source;
      Fill = copyManager.GetCopy(s.Fill);
      Stroke = copyManager.GetCopy(s.Stroke);
      StrokeThickness = s.StrokeThickness;
      StrokeLineJoin = s.StrokeLineJoin;
      Stretch = s.Stretch;
      _geometry = copyManager.GetCopy(s._geometry);
      Attach();
      OnFillBrushPropertyChanged(_fillProperty, null);
      OnStrokeBrushPropertyChanged(_strokeProperty, null);
    }

    #endregion

    void OnStrokeThicknessChanged(AbstractProperty property, object oldValue)
    {
      _performLayout = true;
    }

    void OnFillBrushChanged(IObservable observable)
    {
      _performLayout = true;
    }

    void OnStrokeBrushChanged(IObservable observable)
    {
      _performLayout = true;
    }

    void OnStrokeLineJoinChanged(AbstractProperty property, object oldValue)
    {
      _performLayout = true;
    }

    void OnFillBrushPropertyChanged(AbstractProperty property, object oldValue)
    {
      if (oldValue is Brush)
        ((Brush)oldValue).ObjectChanged -= OnFillBrushChanged;
      if (Fill != null)
        Fill.ObjectChanged += OnFillBrushChanged;
      OnFillBrushChanged(null);
    }

    void OnStrokeBrushPropertyChanged(AbstractProperty property, object oldValue)
    {
      if (oldValue is Brush)
        ((Brush)oldValue).ObjectChanged -= OnStrokeBrushChanged;
      if (Stroke != null)
        Stroke.ObjectChanged += OnStrokeBrushChanged;
      OnStrokeBrushChanged(null);
    }

    public AbstractProperty StretchProperty
    {
      get { return _stretchProperty; }
    }

    public Stretch Stretch
    {
      get { return (Stretch)_stretchProperty.GetValue(); }
      set { _stretchProperty.SetValue(value); }
    }

    public AbstractProperty FillProperty
    {
      get { return _fillProperty; }
    }

    public Brush Fill
    {
      get { return (Brush)_fillProperty.GetValue(); }
      set { _fillProperty.SetValue(value); }
    }

    public AbstractProperty StrokeProperty
    {
      get { return _strokeProperty; }
    }

    public Brush Stroke
    {
      get { return (Brush)_strokeProperty.GetValue(); }
      set { _strokeProperty.SetValue(value); }
    }

    public AbstractProperty StrokeThicknessProperty
    {
      get { return _strokeThicknessProperty; }
    }

    public double StrokeThickness
    {
      get { return (double)_strokeThicknessProperty.GetValue(); }
      set { _strokeThicknessProperty.SetValue(value); }
    }

    public AbstractProperty StrokeLineJoinProperty
    {
      get { return _strokeLineJoinProperty; }
    }

    /// <summary>
    /// Gets or sets a PenLineJoin enumeration value that specifies the type of join that is used at the vertices of a Shape.
    /// </summary>
    public PenLineJoin StrokeLineJoin
    {
      get { return (PenLineJoin)_strokeLineJoinProperty.GetValue(); }
      set { _strokeLineJoinProperty.SetValue(value); }
    }

    protected void PerformLayout(RenderContext context)
    {
      if (!_performLayout)
        return;
      _performLayout = false;
      DoPerformLayout(context);
    }

    /// <summary>
    /// Allocates and initializes the Brushes <see cref="Fill"/> and <see cref="Stroke"/>.
    /// This method will be overridden in sub classes.
    /// </summary>
    protected virtual void DoPerformLayout(RenderContext context)
    {
    }

    public override void RenderOverride(RenderContext localRenderContext)
    {
      lock (_resourceRenderLock)
      {
        base.RenderOverride(localRenderContext);
        PerformLayout(localRenderContext);
        var geometry = _geometry;
        if (geometry == null || geometry.IsDisposed)
          return;

        var fill = Fill;
        if (fill != null && fill.Brush2D != null)
        {
          GraphicsDevice11.Instance.Context2D1.FillGeometry(geometry, fill.Brush2D); // TODO: Opacity brush?
        }
        var stroke = Stroke;
        // TODO: why is stroke.Brush2D null sometimes? Not yet allocated or already disposed?
        if (stroke != null && stroke.Brush2D != null)
        {
          GraphicsDevice11.Instance.Context2D1.DrawGeometry(geometry, stroke.Brush2D, (float)StrokeThickness);
        }
      }
    }

    protected override void ArrangeOverride()
    {
      _performLayout = true;
      base.ArrangeOverride();
    }

    public override void Deallocate()
    {
      lock (_resourceRenderLock)
      {
        base.Deallocate();
        if (Fill != null)
          Fill.Deallocate();
        if (Stroke != null)
          Stroke.Deallocate();
      }
    }

    public override void Allocate()
    {
      base.Allocate();
      if (Fill != null)
        Fill.Allocate();
      if (Stroke != null)
        Stroke.Allocate();
      _performLayout = true;
    }

    protected SharpDX.Direct2D1.Geometry CalculateTransformedPath(SharpDX.Direct2D1.Geometry path, RectangleF baseRect)
    {
      SharpDX.Direct2D1.Geometry result = path;
      Matrix m = Matrix.Identity;
      //RectangleF bounds = result.GetBounds();
      RectangleF bounds = result.GetWidenedBounds((float)StrokeThickness);
      _fillDisabled = bounds.Width < StrokeThickness || bounds.Height < StrokeThickness;
      if (Width > 0) baseRect.Width = (float)Width;
      if (Height > 0) baseRect.Height = (float)Height;
      float scaleW;
      float scaleH;
      if (Stretch == Stretch.Fill)
      {
        scaleW = baseRect.Width / bounds.Width;
        scaleH = baseRect.Height / bounds.Height;
        m *= Matrix.Translation(-bounds.X, -bounds.Y, 0);
      }
      else if (Stretch == Stretch.Uniform)
      {
        scaleW = Math.Min(baseRect.Width / bounds.Width, baseRect.Height / bounds.Height);
        scaleH = scaleW;
        m *= Matrix.Scaling(-bounds.X, -bounds.Y, 1);
      }
      else if (Stretch == Stretch.UniformToFill)
      {
        scaleW = Math.Max(baseRect.Width / bounds.Width, baseRect.Height / bounds.Height);
        scaleH = scaleW;
        m *= Matrix.Translation(-bounds.X, -bounds.Y, 0);
      }
      else
      {
        // Stretch == Stretch.None
        scaleW = 1;
        scaleH = 1;
      }
      // In case bounds.Width or bounds.Height or baseRect.Width or baseRect.Height were 0
      if (scaleW == 0 || float.IsNaN(scaleW) || float.IsInfinity(scaleW)) scaleW = 1;
      if (scaleH == 0 || float.IsNaN(scaleH) || float.IsInfinity(scaleH)) scaleH = 1;
      m *= Matrix.Scaling(scaleW, scaleH, 1);

      m *= Matrix.Translation(baseRect.X, baseRect.Y, 0);

      result = new TransformedGeometry(path.Factory, path, m);
      return result;
    }
  }
}
