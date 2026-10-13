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
using MediaPortal.UI.SkinEngine.Gpu;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Rendering
{
  /// <summary>
  /// Encapsulates a list of vertices which can be rendered with the effect that is currently active
  /// (see <see cref="MediaPortal.UI.SkinEngine.ContentManagement.EffectAsset"/>).
  /// </summary>
  public class PrimitiveBuffer : IDisposable
  {
    #region Protected fields

    protected SKVertices _vertices = null; // Positions and texture coordinates, with vertex colors
    protected SKVertices _verticesNoColors = null; // Positions and texture coordinates only
    protected PositionColoredTextured[] _vertexData = null;
    protected PrimitiveType _primitiveType = PrimitiveType.TriangleList;
    protected int _primitiveCount = 0;
    protected int _vertexCount = 0;
    protected static int _allocationCount = 0;

    #endregion

    #region Ctor & maintainance

    public PrimitiveBuffer()
    {
    }

    /// <summary>
    /// Contructor to intialise with an allocated but empty buffer.
    /// </summary>
    /// <param name="numVertices">The number of vertices to allocate.</param>
    public PrimitiveBuffer(int numVertices)
    {
      Create(numVertices);
    }

    /// <summary>
    /// Constructor to fully initialise the buffer with the passed vertices.
    /// </summary>
    /// <param name="vertices">The vertex data to copy.</param>
    /// <param name="primitiveType">The primitive type to be used when rendering.</param>
    public PrimitiveBuffer(ref PositionColoredTextured[] vertices, PrimitiveType primitiveType)
    {
      Set(ref vertices, primitiveType);
    }

    public void Dispose()
    {
      Clear();
    }

    #endregion

    #region Buffer creation & disposal

    /// <summary>
    /// Allocates an empty vertex buffer.
    /// </summary>
    /// <param name="numVertices">The number of vertices to allocate.</param>
    public void Create(int numVertices)
    {
      if (_vertexData != null)
        Clear();
      _vertexData = new PositionColoredTextured[numVertices];
      _vertexCount = numVertices;
      ++_allocationCount;
    }

    /// <summary>
    /// Copies the given vertex data into the buffer, re-allocating it if necessary.
    /// </summary>
    /// <param name="vertices">The vertex data to copy.</param>
    /// <param name="primitiveType">The primitive type to be used when rendering.</param>
    public void Set(ref PositionColoredTextured[] vertices, PrimitiveType primitiveType)
    {
      if (vertices == null)
      {
        Clear();
        return;
      }
      int numVertices = vertices.Length;
      if (_vertexData == null || numVertices > VertexCount)
        Create(numVertices);

      switch (primitiveType)
      {
        case PrimitiveType.TriangleList:
          if (numVertices < 3 || numVertices % 3 != 0)
            ThrowInvalidVertexCount();
          _primitiveCount = numVertices / 3;
          break;
        case PrimitiveType.TriangleStrip:
        case PrimitiveType.TriangleFan:
          if (numVertices < 3)
            ThrowInvalidVertexCount();
          _primitiveCount = numVertices - 2;
          break;
        default:
          throw new NotImplementedException("PrimitiveType " + primitiveType + " is not supported");
      }
      _primitiveType = primitiveType;
      _vertexCount = numVertices;
      Array.Copy(vertices, _vertexData, numVertices);
      DisposeVertices();
      BuildVertices();
    }

    protected void BuildVertices()
    {
      SKPoint[] positions = new SKPoint[_vertexCount];
      SKPoint[] texCoords = new SKPoint[_vertexCount];
      SKColor[] colors = new SKColor[_vertexCount];
      for (int i = 0; i < _vertexCount; i++)
      {
        PositionColoredTextured v = _vertexData[i];
        positions[i] = new SKPoint(v.X, v.Y);
        texCoords[i] = new SKPoint(v.Tu1, v.Tv1);
        colors[i] = new SKColor((uint) v.Color);
      }
      SKVertexMode mode = _primitiveType.ToVertexMode();
      _vertices = SKVertices.CreateCopy(mode, positions, texCoords, colors);
      _verticesNoColors = SKVertices.CreateCopy(mode, positions, texCoords, null);
    }

    protected void DisposeVertices()
    {
      if (_vertices != null)
        _vertices.Dispose();
      _vertices = null;
      if (_verticesNoColors != null)
        _verticesNoColors.Dispose();
      _verticesNoColors = null;
    }

    protected void Clear()
    {
      DisposeVertices();
      if (_vertexData != null)
      {
        --_allocationCount;
        _vertexData = null;
      }
      _primitiveCount = 0;
      _vertexCount = 0;
    }

    public static void SetPrimitiveBuffer(ref PrimitiveBuffer _buffer, ref PositionColoredTextured[] verts, PrimitiveType type)
    {
      if (_buffer == null)
        _buffer = new PrimitiveBuffer();
      _buffer.Set(ref verts, type);
    }

    public static void DisposePrimitiveBuffer(ref PrimitiveBuffer _buffer)
    {
      if (_buffer != null)
        _buffer.Dispose();
      _buffer = null;
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Renders the primitives with the current draw state (see <see cref="GraphicsDevice.SetDrawState"/>).
    /// </summary>
    /// <param name="stream">Stream number, not used.</param>
    public void Render(int stream)
    {
      SKCanvas canvas = GraphicsDevice.Canvas;
      SKVertices vertices = GraphicsDevice.CurrentUsesVertexColors ? _vertices : _verticesNoColors;
      if (canvas == null || vertices == null)
        return;
      using (SKPaint paint = new SKPaint())
      {
        paint.Shader = GraphicsDevice.CurrentShader;
        paint.BlendMode = GraphicsDevice.SkBlendMode;
        paint.IsAntialias = true;
        paint.FilterQuality = SKFilterQuality.Low; // Bilinear filtering for the textures of the shader
        canvas.SetMatrix(GraphicsDevice.CurrentMatrix);
        canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);
        canvas.ResetMatrix();
      }
      GraphicsDevice.RenderTargetChanged();
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Gets a value indicating the resource is allocated.
    /// </summary>
    public bool IsAllocated
    {
      get { return _vertexData != null; }
    }

    /// <summary>
    /// Gets the number of vertices in this buffer.
    /// </summary>
    public int VertexCount
    {
      get { return _vertexData == null ? 0 : _vertexData.Length; }
    }

    /// <summary>
    /// Gets the number of primitives in this buffer.
    /// </summary>
    public int PrimitiveCount
    {
      get { return _primitiveCount; }
    }

    /// <summary>
    /// Gets the type of the primitives.
    /// </summary>
    public PrimitiveType PrimitiveType
    {
      get { return _primitiveType; }
    }

    /// <summary>
    /// Gets the allocation count of all primitive buffers.
    /// </summary>
    public static int AllocationCount
    {
      get { return _allocationCount; }
    }

    #endregion

    protected static void ThrowInvalidVertexCount()
    {
      throw new ArgumentException("Invalid number of vertices for the given primitive type");
    }
  }
}
