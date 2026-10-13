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
using System.IO;
using System.Text;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Gpu;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SharpDX;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.ContentManagement.AssetCore
{
  /// <summary>
  /// Encapsulates an effect which can render a vertex buffer with a texture.
  /// The effect gets loaded from the skin's shaders directory.
  /// </summary>
  public class EffectAssetCore : TemporaryAssetCoreBase, IAssetCore
  {
    public event AssetAllocationHandler AllocationChanged = delegate { };

    #region Consts

    protected const string PARAM_TEXTURE = "g_texture";
    protected const string PARAM_BORDER_COLOR = "g_bordercolor";
    protected const string PARAM_BORDER_COLOR_A = "g_bordercolorA";
    protected const string SHADER_EXTENSION = ".sksl";

    #endregion

    protected readonly string _effectName;
    protected readonly IDictionary<string, object> _parameterValues;
    protected volatile Effect _effect;
    protected bool _fileMissing = false;
    protected bool _usesVertexColors = false;
    protected bool _renderActive = false;

    /// <summary>
    /// EffectAssetCore constructor.
    /// </summary>
    /// <param name="effectName"> The name of the shader file or a semi-colon seperated list of filenames,
    /// all which will be assumed to be in the directory specified by <see cref="SkinResources.SHADERS_DIRECTORY"/>.
    /// The files will be concatenated in reverse order to create the final effect.</param>
    public EffectAssetCore(string effectName)
    {
      _parameterValues = new Dictionary<string, object>();
      _effectName = effectName;
    }

    public bool Allocate()
    {
      string[] files = _effectName.Split(';');
      if (files.Length == 0)
        return false;

      StringBuilder effectShader = new StringBuilder(8196);
      for (int i = files.Length - 1; i >= 0; --i)
      {
        string effectFilePath = SkinContext.SkinResources.GetResourceFilePath(
            string.Format(@"{0}\{1}{2}", SkinResources.SHADERS_DIRECTORY, files[i], SHADER_EXTENSION));
        if (effectFilePath == null || !File.Exists(effectFilePath))
        {
          if (!_fileMissing)
            ServiceRegistration.Get<ILogger>().Error("Effect file {0} does not exist", effectFilePath);
          _fileMissing = true;
          return false;
        }
        _fileMissing = false;

        using (StreamReader reader = new StreamReader(effectFilePath))
          effectShader.Append(reader.ReadToEnd());

        // Concatenate
        effectShader.Append(Environment.NewLine);
      }

      // We place the lock here to comply to the MP2 multithreading guideline - we are not allowed to request the
      // effect resources when holding our lock
      lock (_syncObj)
      {
        if (_effect != null)
          return true;

        try
        {
          string source = EffectPrelude.Source + effectShader;
          string errors;
          _effect = Effect.FromSource(source, out errors);
          if (_effect == null)
          {
            ServiceRegistration.Get<ILogger>().Error("EffectAsset: Unable to compile '{0}':\n{1}", _effectName, errors);
            return false;
          }
          // Shaders which contain the marker '// vertexcolors' are modulated with the colors of the vertices
          _usesVertexColors = source.Contains("// vertexcolors");
          return true;
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Error("EffectAsset: Unable to load '{0}'", ex, _effectName);
          return false;
        }
      }
    }

    #region Public properties

    public Effect Effect
    {
      get
      {
        if (!IsAllocated)
          Allocate();
        return _effect;
      }
    }

    public IDictionary<string, object> Parameters
    {
      get { return _parameterValues; }
    }

    #endregion

    #region IAssetCore implementation

    public bool IsAllocated
    {
      get { return _effect != null; }
    }

    public int AllocationSize
    {
      get { return 0; }
    }

    public void Free()
    {
      if (_effect != null)
      {
        _effect.Dispose();
        _effect = null;
      }
    }

    #endregion

    /// <summary>
    /// Starts the rendering of the given texture <paramref name="texture"/> in the given <paramref name="stream"/>.
    /// </summary>
    /// <param name="texture">The texture to be rendered. If <c>null</c>, the texture bound to the sampler 0 is used.</param>
    /// <param name="stream">Number of the stream to render.</param>
    /// <param name="finalTransform">Final render transformation to apply.</param>
    public void StartRender(Texture texture, int stream, Matrix finalTransform)
    {
      if (!IsAllocated)
      {
        Allocate();
        if (!IsAllocated)
          return;
      }

      SamplerState sampler = GraphicsDevice.GetSamplerState(0);
      _effect.SetTexture(PARAM_TEXTURE, texture ?? GraphicsDevice.GetTexture(0), sampler.AddressU, sampler.AddressV);
      _effect.SetValue(PARAM_BORDER_COLOR, ToStraightColor(sampler.BorderColor));
      _effect.SetValue(PARAM_BORDER_COLOR_A, ToStraightColor(GraphicsDevice.GetSamplerState(1).BorderColor));
      SetEffectParameters();
      GraphicsDevice.SetDrawState(_effect.CreateShader(), finalTransform, _usesVertexColors);
      _renderActive = true;

      KeepAlive();
    }

    /// <summary>
    /// Ends the rendering of the given <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">Number of the stream to end the rendering.</param>
    public void EndRender(int stream)
    {
      if (!_renderActive)
        return;
      _renderActive = false;
      SKShader shader = GraphicsDevice.CurrentShader;
      GraphicsDevice.ClearDrawState();
      if (shader != null)
        shader.Dispose();
      if (_effect != null)
        _effect.ReleaseTemporaries();
    }

    /// <summary>
    /// Converts a color in BGRA integer layout (ARGB, as used for the sampler border color) to a straight RGBA color.
    /// </summary>
    protected static Color4 ToStraightColor(int bgra)
    {
      uint value = (uint) bgra;
      return new Color4(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, ((value >> 24) & 0xFF) / 255f);
    }

    public void SetEffectParameters()
    {
      if (_parameterValues.Count == 0 | !IsAllocated)
        return;
      SamplerState sampler = GraphicsDevice.GetSamplerState(1);
      foreach (KeyValuePair<string, object> kvp in _parameterValues)
      {
        object value = kvp.Value;
        if (value is Texture)
          _effect.SetTexture(kvp.Key, (Texture) value, sampler.AddressU, sampler.AddressV);
        else if (value is Color4)
          _effect.SetValue(kvp.Key, (Color4) value);
        else if (value is Color4[])
          _effect.SetValue(kvp.Key, (Color4[]) value);
        else if (value is float)
          _effect.SetValue(kvp.Key, (float) value);
        else if (value is float[])
          _effect.SetValue(kvp.Key, (float[]) value);
        else if (value is Matrix)
          _effect.SetValue(kvp.Key, (Matrix) value);
        else if (value is Vector2)
          _effect.SetValue(kvp.Key, (Vector2) value);
        else if (value is Vector3)
          _effect.SetValue(kvp.Key, (Vector3) value);
        else if (value is Vector4)
          _effect.SetValue(kvp.Key, (Vector4) value);
        else if (value is bool)
          _effect.SetValue(kvp.Key, (bool) value);
        else if (value is int)
          _effect.SetValue(kvp.Key, (int) value);
      }
    }
  }
}
