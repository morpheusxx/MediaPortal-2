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
using SharpDX;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.Gpu
{
  /// <summary>
  /// A shader effect, compiled from SkSL source (Skia runtime effect). Uniform values and textures are set by name.
  /// Textures are declared in the SkSL source as <c>uniform shader name;</c> and are sampled with normalized (0..1) texture
  /// coordinates scaled by <c>name.eval(uv * name_size)</c>, where <c>name_size</c> is an optional <c>uniform float2</c>.
  /// </summary>
  public class Effect : IDisposable
  {
    protected readonly SKRuntimeEffect _effect;
    protected readonly SKRuntimeEffectUniforms _uniforms;
    protected readonly SKRuntimeEffectChildren _children;
    protected readonly HashSet<string> _uniformNames = new HashSet<string>();
    protected readonly HashSet<string> _childNames = new HashSet<string>();
    protected readonly List<SKShader> _ownedShaders = new List<SKShader>();

    protected Effect(SKRuntimeEffect effect)
    {
      _effect = effect;
      _uniforms = new SKRuntimeEffectUniforms(effect);
      _children = new SKRuntimeEffectChildren(effect);
      foreach (string name in effect.Uniforms)
        _uniformNames.Add(name);
      foreach (string name in effect.Children)
        _childNames.Add(name);
    }

    /// <summary>
    /// Compiles the given SkSL source. Returns <c>null</c> and the compiler messages in <paramref name="errors"/> if the source is invalid.
    /// </summary>
    public static Effect FromSource(string sksl, out string errors)
    {
      SKRuntimeEffect effect = SKRuntimeEffect.Create(sksl, out errors);
      return effect == null ? null : new Effect(effect);
    }

    public bool HasUniform(string name)
    {
      return _uniformNames.Contains(name);
    }

    public bool HasTexture(string name)
    {
      return _childNames.Contains(name);
    }

    #region SetValue overloads

    public void SetValue(string name, float value)
    {
      if (HasUniform(name))
        _uniforms[name] = value;
    }

    public void SetValue(string name, int value)
    {
      if (HasUniform(name))
        _uniforms[name] = (float) value;
    }

    public void SetValue(string name, bool value)
    {
      if (HasUniform(name))
        _uniforms[name] = value ? 1f : 0f;
    }

    public void SetValue(string name, float[] value)
    {
      if (HasUniform(name))
        _uniforms[name] = value;
    }

    public void SetValue(string name, Vector2 value)
    {
      SetValue(name, new[] { value.X, value.Y });
    }

    public void SetValue(string name, Vector3 value)
    {
      SetValue(name, new[] { value.X, value.Y, value.Z });
    }

    public void SetValue(string name, Vector4 value)
    {
      SetValue(name, new[] { value.X, value.Y, value.Z, value.W });
    }

    /// <summary>
    /// Sets a color. The color is passed as straight (not premultiplied) RGBA.
    /// </summary>
    public void SetValue(string name, Color4 value)
    {
      SetValue(name, new[] { value.Red, value.Green, value.Blue, value.Alpha });
    }

    public void SetValue(string name, Color4[] value)
    {
      float[] data = new float[value.Length * 4];
      for (int i = 0; i < value.Length; i++)
      {
        data[4 * i] = value[i].Red;
        data[4 * i + 1] = value[i].Green;
        data[4 * i + 2] = value[i].Blue;
        data[4 * i + 3] = value[i].Alpha;
      }
      SetValue(name, data);
    }

    /// <summary>
    /// Sets a 4x4 matrix in row-major order.
    /// </summary>
    public void SetValue(string name, Matrix value)
    {
      SetValue(name, new[]
        {
          value.M11, value.M12, value.M13, value.M14,
          value.M21, value.M22, value.M23, value.M24,
          value.M31, value.M32, value.M33, value.M34,
          value.M41, value.M42, value.M43, value.M44
        });
    }

    #endregion

    /// <summary>
    /// Sets the texture to be sampled by the child shader <paramref name="name"/>. For the texture size the optional uniform
    /// <c>name_size</c> (float2) is set.
    /// </summary>
    public void SetTexture(string name, Texture texture, TextureAddress addressU, TextureAddress addressV)
    {
      if (!HasTexture(name))
        return;
      SKShader shader = texture == null ? null : texture.CreateShaderInPixels(addressU, addressV);
      _children[name] = shader ?? SKShader.CreateColor(SKColors.Transparent);
      if (shader != null)
        _ownedShaders.Add(shader);
      if (texture != null)
        SetValue(name + "_size", new Vector2(texture.Width, texture.Height));
    }

    /// <summary>
    /// Creates the shader for the current values. The shader must be disposed by the caller.
    /// </summary>
    public SKShader CreateShader()
    {
      return _effect.ToShader(false, _uniforms, _children);
    }

    /// <summary>
    /// Releases the shaders which were created for the textures since the last call. Has to be called after the draw calls were issued.
    /// </summary>
    public void ReleaseTemporaries()
    {
      foreach (SKShader shader in _ownedShaders)
        shader.Dispose();
      _ownedShaders.Clear();
    }

    public void Dispose()
    {
      ReleaseTemporaries();
      _effect.Dispose();
    }
  }
}
