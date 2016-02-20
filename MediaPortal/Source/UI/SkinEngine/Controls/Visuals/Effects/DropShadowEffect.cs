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

using System.Collections.Generic;
using MediaPortal.Common.General;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.UI.SkinEngine.Controls.Visuals.Effects
{
  public class DropShadowEffect : ImageEffect
  {
    #region Consts

    public const string EFFECT_DROPSHADOW = "dropshadow";

    #endregion

    #region Protected fields

    protected AbstractProperty _offsetXProperty;
    protected AbstractProperty _offsetYProperty;
    protected AbstractProperty _angleProperty;
    protected AbstractProperty _alphaProperty;

    protected Dictionary<string, object> _effectParameters = new Dictionary<string, object>();

    #endregion

    #region Ctor & maintainance

    public DropShadowEffect()
    {
      _partialShaderEffect = EFFECT_DROPSHADOW;
      Init();
    }

    void Init()
    {
      _offsetXProperty = new SProperty(typeof(float), 4.0f);
      _offsetYProperty = new SProperty(typeof(float), 4.0f);
      _angleProperty = new SProperty(typeof(float), 0.0f);
      _alphaProperty = new SProperty(typeof(float), 0.5f);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      base.DeepCopy(source, copyManager);
      DropShadowEffect el = (DropShadowEffect)source;
      OffsetX = el.OffsetX;
      OffsetY = el.OffsetY;
      Alpha = el.Alpha;
      Angle = el.Angle;
    }

    #endregion

    #region Properties

    public AbstractProperty OffsetXProperty
    {
      get { return _offsetXProperty; }
    }

    public float OffsetX
    {
      get { return (float)_offsetXProperty.GetValue(); }
      set { _offsetXProperty.SetValue(value); }
    }

    public AbstractProperty OffsetYProperty
    {
      get { return _offsetYProperty; }
    }

    public float OffsetY
    {
      get { return (float)_offsetYProperty.GetValue(); }
      set { _offsetYProperty.SetValue(value); }
    }

    public AbstractProperty AngleProperty
    {
      get { return _angleProperty; }
    }

    public float Angle
    {
      get { return (float)_angleProperty.GetValue(); }
      set { _angleProperty.SetValue(value); }
    }
    public AbstractProperty AlphaProperty
    {
      get { return _alphaProperty; }
    }

    public float Alpha
    {
      get { return (float)_alphaProperty.GetValue(); }
      set { _alphaProperty.SetValue(value); }
    }



    #endregion

    protected override Dictionary<string, object> GetShaderParameters()
    {
      _effectParameters["g_offsetX"] = OffsetX;
      _effectParameters["g_offsetY"] = OffsetY;
      _effectParameters["g_alpha"] = Alpha;
      _effectParameters["g_angle"] = Angle;
      return _effectParameters;
    }
  }
}
