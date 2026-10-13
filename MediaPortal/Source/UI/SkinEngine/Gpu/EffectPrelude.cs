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

namespace MediaPortal.UI.SkinEngine.Gpu
{
  /// <summary>
  /// SkSL source which is prepended to every effect which is loaded from the skin's shader directory. It declares the textures and
  /// provides the sampling functions, so the effect files only contain the actual effect.
  /// </summary>
  /// <remarks>
  /// <para>
  /// All sampling functions take normalized (0..1) texture coordinates and return straight (not premultiplied) colors, like the
  /// former HLSL shaders. Effects return a premultiplied color from <c>main</c>.
  /// </para>
  /// <para>
  /// The texture uniforms are set by <see cref="MediaPortal.UI.SkinEngine.ContentManagement.AssetCore.EffectAssetCore"/>:
  /// <c>g_texture</c> is the texture of the effect, <c>g_textureA</c> the second texture of transitions and <c>g_bordercolor</c> the
  /// color for the area outside of the texture (sampler border color).
  /// </para>
  /// </remarks>
  public static class EffectPrelude
  {
    public const string Source = @"
uniform shader g_texture;
uniform float2 g_texture_size;
uniform shader g_textureA;
uniform float2 g_textureA_size;
uniform float4 g_bordercolor;
uniform float4 g_bordercolorA;

float4 Premul(float4 c)
{
  return float4(c.rgb * c.a, c.a);
}

float4 Unpremul(float4 c)
{
  if (c.a > 0.0)
    c.rgb = c.rgb / c.a;
  return c;
}

// Samples g_texture, coordinates outside of the texture are clamped to the border
float4 SampleClamped(float2 uv)
{
  return Unpremul(float4(g_texture.eval(uv * g_texture_size)));
}

// Samples a one dimensional gradient texture
float4 SampleGradient(float dist)
{
  return Unpremul(float4(g_texture.eval(float2(dist * g_texture_size.x, 0.5 * g_texture_size.y))));
}

// Samples g_texture, the area outside of the texture is transparent
float4 SampleDecal(float2 uv)
{
  if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
    return float4(0.0);
  return SampleClamped(uv);
}

// Samples g_texture, the area outside of the texture has the border color
float4 SampleBorder(float2 uv)
{
  if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
    return g_bordercolor;
  return SampleClamped(uv);
}

// Samples g_textureA, the area outside of the texture has the border color
float4 SampleBorderA(float2 uv)
{
  if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
    return g_bordercolorA;
  return Unpremul(float4(g_textureA.eval(uv * g_textureA_size)));
}

// Samples the texture which is selected by the parameter: 0 = g_texture, 1 = g_textureA
float4 SampleWhich(int which, float2 uv)
{
  if (which == 0)
    return SampleBorder(uv);
  return SampleBorderA(uv);
}
";
  }
}
