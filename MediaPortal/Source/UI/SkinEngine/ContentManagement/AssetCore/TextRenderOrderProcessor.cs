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

using System.Text;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using Factory = SharpDX.DirectWrite.Factory;

namespace MediaPortal.UI.SkinEngine.ContentManagement.AssetCore
{
  public class TextRenderOrderProcessor: TextRendererBase
  {
    protected const float MAX_TEXT_WIDTH = 1024;
    protected const float MAX_TEXT_HEIGHT = 1024;
    protected readonly Factory _dwFactory;
    protected readonly TextFormat _textFormat;
    protected StringBuilder _sb;

    public TextRenderOrderProcessor()
    {
      _dwFactory = new Factory();
      _textFormat = new TextFormat(_dwFactory, "Arial", 48)
      {
        TextAlignment = TextAlignment.Center,
        ParagraphAlignment = ParagraphAlignment.Center
      };
    }

    public string GetProcessedText(string text)
    {
      _sb = new StringBuilder();
      TextLayout layout = new TextLayout(_dwFactory, text, _textFormat, MAX_TEXT_WIDTH, MAX_TEXT_HEIGHT);
      layout.Draw(this, 0, 0);
      return _sb.ToString();
    }
    
    public override Result DrawGlyphRun(object clientDrawingContext, float baselineOriginX, float baselineOriginY, MeasuringMode measuringMode, GlyphRun glyphRun, GlyphRunDescription glyphRunDescription, ComObject clientDrawingEffect)
    {
      var textPart = glyphRunDescription.Text;
      _sb.Append(glyphRun.BidiLevel % 2 == 1 ? Reverse(textPart) : textPart);
      return Result.Ok;
    }

    static string Reverse(string text)
    {
      char[] reversed = new char[text.Length];
      for (int i = text.Length - 1; i >= 0; i--)
        reversed[text.Length - 1 - i] = text[i];
      return new string(reversed);
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing)
      {
        _textFormat.Dispose();
        _dwFactory.Dispose();
      }
    }
  }
}
