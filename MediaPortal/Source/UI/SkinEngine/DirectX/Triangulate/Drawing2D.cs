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

namespace MediaPortal.UI.SkinEngine.DirectX.Triangulate
{
  // Portable replacements for the parts of System.Drawing.Drawing2D which are used by the shape/border tessellation.
  // System.Drawing.Common is only available on Windows.

  public enum FillMode
  {
    Alternate = 0,
    Winding = 1
  }

  public enum MatrixOrder
  {
    Prepend = 0,
    Append = 1
  }

  /// <summary>
  /// 2D affine transformation matrix with the same semantics as MediaPortal.UI.SkinEngine.DirectX.Triangulate.Matrix2D
  /// (row vectors: <c>[x' y'] = [x y 1] * M</c>).
  /// </summary>
  public class Matrix2D : IDisposable
  {
    private float _m11 = 1, _m12 = 0, _m21 = 0, _m22 = 1, _dx = 0, _dy = 0;

    public Matrix2D()
    { }

    public Matrix2D(float m11, float m12, float m21, float m22, float dx, float dy)
    {
      _m11 = m11;
      _m12 = m12;
      _m21 = m21;
      _m22 = m22;
      _dx = dx;
      _dy = dy;
    }

    public float[] Elements
    {
      get { return new[] { _m11, _m12, _m21, _m22, _dx, _dy }; }
    }

    private void Multiply(Matrix2D other, MatrixOrder order)
    {
      // result = this * other (Append) or other * this (Prepend)
      Matrix2D a = order == MatrixOrder.Append ? this : other;
      Matrix2D b = order == MatrixOrder.Append ? other : this;
      float m11 = a._m11 * b._m11 + a._m12 * b._m21;
      float m12 = a._m11 * b._m12 + a._m12 * b._m22;
      float m21 = a._m21 * b._m11 + a._m22 * b._m21;
      float m22 = a._m21 * b._m12 + a._m22 * b._m22;
      float dx = a._dx * b._m11 + a._dy * b._m21 + b._dx;
      float dy = a._dx * b._m12 + a._dy * b._m22 + b._dy;
      _m11 = m11;
      _m12 = m12;
      _m21 = m21;
      _m22 = m22;
      _dx = dx;
      _dy = dy;
    }

    public void Translate(float offsetX, float offsetY, MatrixOrder order = MatrixOrder.Prepend)
    {
      Multiply(new Matrix2D(1, 0, 0, 1, offsetX, offsetY), order);
    }

    public void Scale(float scaleX, float scaleY, MatrixOrder order = MatrixOrder.Prepend)
    {
      Multiply(new Matrix2D(scaleX, 0, 0, scaleY, 0, 0), order);
    }

    public void Rotate(float angleDegrees, MatrixOrder order = MatrixOrder.Prepend)
    {
      double rad = angleDegrees * Math.PI / 180.0;
      float cos = (float) Math.Cos(rad);
      float sin = (float) Math.Sin(rad);
      Multiply(new Matrix2D(cos, sin, -sin, cos, 0, 0), order);
    }

    public void RotateAt(float angleDegrees, PointF center, MatrixOrder order = MatrixOrder.Prepend)
    {
      if (order == MatrixOrder.Prepend)
      {
        Translate(center.X, center.Y, MatrixOrder.Prepend);
        Rotate(angleDegrees, MatrixOrder.Prepend);
        Translate(-center.X, -center.Y, MatrixOrder.Prepend);
      }
      else
      {
        Translate(-center.X, -center.Y, MatrixOrder.Append);
        Rotate(angleDegrees, MatrixOrder.Append);
        Translate(center.X, center.Y, MatrixOrder.Append);
      }
    }

    public PointF Transform(PointF p)
    {
      return new PointF(p.X * _m11 + p.Y * _m21 + _dx, p.X * _m12 + p.Y * _m22 + _dy);
    }

    public void Dispose()
    { }
  }

  /// <summary>
  /// A path which consists of figures of flattened (polyline) segments.
  /// </summary>
  /// <remarks>
  /// Curves (arcs, béziers, ellipses) are flattened when they are added, so <see cref="Flatten"/> doesn't need to do anything.
  /// </remarks>
  public class GraphicsPath : IDisposable
  {
    #region Figure class

    internal class Figure
    {
      public readonly List<PointF> Points = new List<PointF>();
      public bool Closed;
    }

    #endregion

    private const float FLATNESS = 0.25f;

    private readonly List<Figure> _figures = new List<Figure>();
    private Figure _current = null; // The figure which is continued by the next added segment, or null to start a new figure

    public GraphicsPath() : this(FillMode.Alternate)
    { }

    public GraphicsPath(FillMode fillMode)
    {
      FillMode = fillMode;
    }

    public FillMode FillMode { get; set; }

    internal IList<Figure> Figures
    {
      get { return _figures; }
    }

    #region Figure handling

    private Figure BeginFigure()
    {
      Figure result = new Figure();
      _figures.Add(result);
      return result;
    }

    /// <summary>
    /// Continues the current figure at <paramref name="start"/> or starts a new one. GDI+ connects the last point of the current
    /// figure with the start point of an added segment with a line.
    /// </summary>
    private Figure ContinueFigure(PointF start)
    {
      Figure figure = _current;
      if (figure == null || figure.Closed)
      {
        figure = _current = BeginFigure();
        figure.Points.Add(start);
        return figure;
      }
      PointF last = figure.Points[figure.Points.Count - 1];
      if (last != start)
        figure.Points.Add(start);
      return figure;
    }

    public void StartFigure()
    {
      _current = null;
    }

    public void CloseFigure()
    {
      if (_current != null)
        _current.Closed = true;
      _current = null;
    }

    public void CloseAllFigures()
    {
      foreach (Figure figure in _figures)
        figure.Closed = true;
      _current = null;
    }

    #endregion

    #region Adding segments

    public void AddLine(PointF pt1, PointF pt2)
    {
      Figure figure = ContinueFigure(pt1);
      figure.Points.Add(pt2);
    }

    public void AddLine(float x1, float y1, float x2, float y2)
    {
      AddLine(new PointF(x1, y1), new PointF(x2, y2));
    }

    public void AddLines(PointF[] points)
    {
      if (points == null || points.Length == 0)
        return;
      Figure figure = ContinueFigure(points[0]);
      for (int i = 1; i < points.Length; i++)
        figure.Points.Add(points[i]);
    }

    public void AddRectangle(RectangleF rect)
    {
      AddPolygon(new[]
        {
          new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top),
          new PointF(rect.Right, rect.Bottom), new PointF(rect.Left, rect.Bottom)
        });
    }

    public void AddRectangle(Rectangle rect)
    {
      AddRectangle(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height));
    }

    public void AddPolygon(PointF[] points)
    {
      if (points == null || points.Length == 0)
        return;
      CloseFigure();
      Figure figure = BeginFigure();
      figure.Points.AddRange(points);
      figure.Closed = true;
      _current = null;
    }

    public void AddEllipse(RectangleF rect)
    {
      CloseFigure();
      Figure figure = BeginFigure();
      AddArcPoints(figure, rect, 0, 360, true);
      // The last point of a full circle equals the first one
      figure.Points.RemoveAt(figure.Points.Count - 1);
      figure.Closed = true;
      _current = null;
    }

    public void AddEllipse(float x, float y, float width, float height)
    {
      AddEllipse(new RectangleF(x, y, width, height));
    }

    /// <summary>
    /// Adds an elliptical arc. Angles are in degrees, measured clockwise from the x axis (like in GDI+).
    /// </summary>
    public void AddArc(RectangleF rect, float startAngle, float sweepAngle)
    {
      if (rect.Width <= 0 || rect.Height <= 0)
        return;
      PointF start = ArcPoint(rect, startAngle);
      Figure figure = ContinueFigure(start);
      AddArcPoints(figure, rect, startAngle, sweepAngle, false);
    }

    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
      AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);
    }

    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
      Figure figure = ContinueFigure(pt1);
      float length = Distance(pt1, pt2) + Distance(pt2, pt3) + Distance(pt3, pt4);
      int segments = Math.Max(4, Math.Min(64, (int) Math.Ceiling(Math.Sqrt(length / FLATNESS) / 1.5)));
      for (int i = 1; i <= segments; i++)
      {
        float t = i / (float) segments;
        float u = 1 - t;
        float b0 = u * u * u;
        float b1 = 3 * u * u * t;
        float b2 = 3 * u * t * t;
        float b3 = t * t * t;
        figure.Points.Add(new PointF(
            b0 * pt1.X + b1 * pt2.X + b2 * pt3.X + b3 * pt4.X,
            b0 * pt1.Y + b1 * pt2.Y + b2 * pt3.Y + b3 * pt4.Y));
      }
    }

    public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
      AddBezier(new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));
    }

    /// <summary>
    /// Adds the figures of the given path.
    /// </summary>
    public void AddPath(GraphicsPath addingPath, bool connect)
    {
      bool first = true;
      foreach (Figure source in addingPath._figures)
      {
        if (source.Points.Count == 0)
          continue;
        if (first && connect && _current != null && !_current.Closed)
        {
          _current.Points.AddRange(source.Points);
          _current.Closed = source.Closed;
          if (source.Closed)
            _current = null;
        }
        else
        {
          Figure figure = BeginFigure();
          figure.Points.AddRange(source.Points);
          figure.Closed = source.Closed;
          _current = source.Closed ? null : figure;
        }
        first = false;
      }
    }

    #endregion

    #region Arc helpers

    private static float Distance(PointF a, PointF b)
    {
      float dx = a.X - b.X;
      float dy = a.Y - b.Y;
      return (float) Math.Sqrt(dx * dx + dy * dy);
    }

    private static PointF ArcPoint(RectangleF rect, float angleDegrees)
    {
      double rad = angleDegrees * Math.PI / 180.0;
      float rx = rect.Width / 2;
      float ry = rect.Height / 2;
      return new PointF(rect.Left + rx + rx * (float) Math.Cos(rad), rect.Top + ry + ry * (float) Math.Sin(rad));
    }

    private static void AddArcPoints(Figure figure, RectangleF rect, float startAngle, float sweepAngle, bool includeStart)
    {
      sweepAngle = Math.Max(-360f, Math.Min(360f, sweepAngle));
      float radius = Math.Max(rect.Width, rect.Height) / 2;
      // Angle step so that the chord error is less than the flatness
      double step = radius <= FLATNESS ? Math.PI / 2 : 2 * Math.Acos(1 - FLATNESS / radius);
      int segments = Math.Max(4, (int) Math.Ceiling(Math.Abs(sweepAngle) * Math.PI / 180.0 / step));
      segments = Math.Min(segments, 360);
      if (includeStart)
        figure.Points.Add(ArcPoint(rect, startAngle));
      for (int i = 1; i <= segments; i++)
        figure.Points.Add(ArcPoint(rect, startAngle + sweepAngle * i / segments));
    }

    #endregion

    #region Query and transformation

    /// <summary>
    /// The curves of the path are always stored flattened.
    /// </summary>
    public void Flatten()
    { }

    public int PointCount
    {
      get
      {
        int result = 0;
        foreach (Figure figure in _figures)
          result += figure.Points.Count;
        return result;
      }
    }

    /// <summary>
    /// Gets the points of all figures.
    /// </summary>
    public PointF[] PathPoints
    {
      get
      {
        List<PointF> result = new List<PointF>(PointCount);
        foreach (Figure figure in _figures)
          result.AddRange(figure.Points);
        return result.ToArray();
      }
    }

    public RectangleF GetBounds()
    {
      return GetBounds(null);
    }

    public RectangleF GetBounds(Matrix2D matrix)
    {
      float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
      bool any = false;
      foreach (Figure figure in _figures)
        foreach (PointF source in figure.Points)
        {
          PointF p = matrix == null ? source : matrix.Transform(source);
          minX = Math.Min(minX, p.X);
          minY = Math.Min(minY, p.Y);
          maxX = Math.Max(maxX, p.X);
          maxY = Math.Max(maxY, p.Y);
          any = true;
        }
      return any ? new RectangleF(minX, minY, maxX - minX, maxY - minY) : RectangleF.Empty;
    }

    public void Transform(Matrix2D matrix)
    {
      foreach (Figure figure in _figures)
        for (int i = 0; i < figure.Points.Count; i++)
          figure.Points[i] = matrix.Transform(figure.Points[i]);
    }

    #endregion

    public void Dispose()
    { }
  }

  /// <summary>
  /// Enumerates the subpaths (figures) of a <see cref="GraphicsPath"/>.
  /// </summary>
  public class GraphicsPathIterator : IDisposable
  {
    private readonly GraphicsPath _path;
    private int _next = 0;

    public GraphicsPathIterator(GraphicsPath path)
    {
      _path = path;
    }

    public int SubpathCount
    {
      get
      {
        int count = 0;
        foreach (GraphicsPath.Figure figure in _path.Figures)
          if (figure.Points.Count > 0)
            count++;
        return count;
      }
    }

    /// <summary>
    /// Copies the next subpath into <paramref name="subPath"/>, which is cleared before. Returns the number of points of the subpath
    /// or 0 if there are no more subpaths.
    /// </summary>
    public int NextSubpath(GraphicsPath subPath, out bool isClosed)
    {
      isClosed = false;
      while (_next < _path.Figures.Count)
      {
        GraphicsPath.Figure figure = _path.Figures[_next++];
        if (figure.Points.Count == 0)
          continue;
        subPath.Figures.Clear();
        GraphicsPath.Figure copy = new GraphicsPath.Figure();
        copy.Points.AddRange(figure.Points);
        copy.Closed = figure.Closed;
        subPath.Figures.Add(copy);
        isClosed = figure.Closed;
        return copy.Points.Count;
      }
      return 0;
    }

    public void Dispose()
    { }
  }
}
