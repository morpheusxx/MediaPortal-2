#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
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
using MediaPortal.UI.SkinEngine.DirectX;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using Factory = SharpDX.DirectWrite.Factory;

namespace MediaPortal.UI.SkinEngine.ContentManagement.AssetCore
{
  public class DirectWriteRenderer : TextRendererBase
  {
    protected const float MAX_TEXT_WIDTH = 4096; // Max line width before text is truncated, it must be larger than possible visible range to allow scrolling
    protected const float MAX_TEXT_HEIGHT = 4096; // ... and height.
    protected FontAssetCore _fontAssetCore; // The calling parent
    protected List<PositionColoredTextured> _verts;

    protected readonly Factory _dwFactory;
    protected float _sizeScale;
    protected bool _kerning;
    protected float _y;
    protected float _boxWidth;
    protected string _fontName;

    public DirectWriteRenderer(FontAssetCore fontAssetCore, string fontName)
    {
      _fontAssetCore = fontAssetCore;
      _dwFactory = new Factory();
      _fontName = fontName;
    }

    public float CreateTextLine(string line, float y, float size, float sizeScale, bool kerning, ref List<PositionColoredTextured> verts)
    {
      _verts = verts;
      _sizeScale = sizeScale;
      _kerning = kerning;
      _y = y;
      using (var textFormat = new TextFormat(_dwFactory, _fontName, size) { TextAlignment = TextAlignment.Leading, ParagraphAlignment = ParagraphAlignment.Center })
      {
        TextLayout layout = new TextLayout(_dwFactory, line, textFormat, MAX_TEXT_WIDTH, MAX_TEXT_HEIGHT);
        // Draw will invoke DrawGlyphRun callbacks where the actual vertexes are created
        layout.Draw(this, 0, 0);

        // Make sure there is at least one character
        if (_verts.Count == 0)
        {
          BitmapCharacter c = _fontAssetCore.Character(' ');
          _fontAssetCore.CreateQuad(c, _sizeScale, c.XOffset, y, 1, ref _verts);
        }
        return layout.Metrics.WidthIncludingTrailingWhitespace;
      }
    }

    public override Result DrawGlyphRun(object clientDrawingContext, float baselineOriginX, float baselineOriginY, MeasuringMode measuringMode, GlyphRun glyphRun, GlyphRunDescription glyphRunDescription, ComObject clientDrawingEffect)
    {
      var textPart = glyphRunDescription.Text;
      // The implicit resolved bidi level of the run. Odd levels indicate right-to-left languages like Hebrew and Arabic,
      // while even levels indicate left-to-right languages like English and Japanese (when written horizontally).
      // For right-to-left languages, the text origin is on the right, and text should be drawn to the left.
      bool isRTL = glyphRun.BidiLevel % 2 == 1;
      int direction = isRTL ? -1 : 1;
      float x = baselineOriginX;

      BitmapCharacter lastChar = null;
      foreach (char character in textPart)
      {
        BitmapCharacter c = _fontAssetCore.Character(character);
        // Adjust for kerning
        if (_kerning && lastChar != null)
          x += _fontAssetCore.GetKerningAmount(lastChar, character) * direction;
        lastChar = c;
        if (!char.IsWhiteSpace(character))
          _fontAssetCore.CreateQuad(c, _sizeScale, isRTL ? x - c.Width : x, _y, direction, ref _verts);

        x += c.XAdvance * direction;
      }
      return Result.Ok;
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing)
        _dwFactory.Dispose();
    }
  }
}
