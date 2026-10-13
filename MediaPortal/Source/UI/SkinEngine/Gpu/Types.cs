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
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Gpu
{
  /// <summary>
  /// Pixel formats used by the SkinEngine. Replacement for the Direct3D 9 format enumeration; only the formats which are
  /// really used for UI resources are available.
  /// </summary>
  public enum Format
  {
    Unknown = 0,
    A8R8G8B8,
    X8R8G8B8,
    A8B8G8R8
  }

  /// <summary>
  /// Primitive types which can be rendered by <see cref="MediaPortal.UI.SkinEngine.Rendering.PrimitiveBuffer"/>.
  /// </summary>
  public enum PrimitiveType
  {
    PointList = 1,
    LineList = 2,
    LineStrip = 3,
    TriangleList = 4,
    TriangleStrip = 5,
    TriangleFan = 6
  }

  [Flags]
  public enum Usage
  {
    None = 0,
    RenderTarget = 1,
    Dynamic = 2
  }

  /// <summary>
  /// Texture addressing mode outside of the [0..1] texture coordinate range.
  /// </summary>
  public enum TextureAddress
  {
    Wrap = 1,
    Mirror = 2,
    Clamp = 3,
    Border = 4
  }

  /// <summary>
  /// Texture sampling state of a sampler.
  /// </summary>
  public struct SamplerState
  {
    public TextureAddress AddressU;
    public TextureAddress AddressV;

    /// <summary>
    /// Color in BGRA (ARGB integer) layout which is used outside of the texture when the address mode is <see cref="TextureAddress.Border"/>.
    /// </summary>
    public int BorderColor;

    public static SamplerState Default
    {
      get { return new SamplerState { AddressU = TextureAddress.Clamp, AddressV = TextureAddress.Clamp, BorderColor = 0 }; }
    }
  }

  /// <summary>
  /// Size of an image file.
  /// </summary>
  public struct ImageInformation
  {
    public int Width;
    public int Height;
  }

  /// <summary>
  /// Display mode of the monitor the window is shown on.
  /// </summary>
  public struct DisplayMode
  {
    public int Width;
    public int Height;
    public int RefreshRate;
    public Format Format;

    public override string ToString()
    {
      return string.Format("{0}x{1}@{2}", Width, Height, RefreshRate);
    }
  }

  public struct SurfaceDescription
  {
    public int Width;
    public int Height;
    public Format Format;
  }

  internal static class GpuExtensions
  {
    public static SKColorType ToColorType(this Format format)
    {
      switch (format)
      {
        case Format.A8B8G8R8:
          return SKColorType.Rgba8888;
        default:
          return SKColorType.Bgra8888;
      }
    }

    public static SKShaderTileMode ToTileMode(this TextureAddress address)
    {
      switch (address)
      {
        case TextureAddress.Wrap:
          return SKShaderTileMode.Repeat;
        case TextureAddress.Mirror:
          return SKShaderTileMode.Mirror;
        case TextureAddress.Border:
          return SKShaderTileMode.Decal;
        default:
          return SKShaderTileMode.Clamp;
      }
    }

    public static SKVertexMode ToVertexMode(this PrimitiveType type)
    {
      switch (type)
      {
        case PrimitiveType.TriangleStrip:
          return SKVertexMode.TriangleStrip;
        case PrimitiveType.TriangleFan:
          return SKVertexMode.TriangleFan;
        default:
          return SKVertexMode.Triangles;
      }
    }
  }
}
