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
using MediaPortal.UI.SkinEngine.Fonts;
using SkiaSharp;

namespace MediaPortal.UI.SkinEngine.ContentManagement.AssetCore
{
  /// <summary>
  /// Provides the metrics of a font of a specific size and creates the Skia fonts which are used to draw text.
  /// </summary>
  /// <remarks>
  /// All metrics are measured with the "rendered size" of this asset and are scaled linearly for other font sizes, so layout is
  /// independent of the size which is finally used to draw a text.
  /// </remarks>
  public class FontAssetCore : TemporaryAssetCoreBase, IAssetCore
  {
    public event AssetAllocationHandler AllocationChanged = delegate { };

    private struct CharMetrics
    {
      public float Advance;
      public float Overhang; // Width of the glyph which exceeds its advance width
      public float Descent; // Height of the glyph below the baseline
    }

    protected FontFamily _family;
    protected readonly int _renderedSize;
    protected readonly uint _resolution;
    protected float _ascender;
    protected SKFont _measureFont;
    private readonly Dictionary<char, CharMetrics> _charMetrics = new Dictionary<char, CharMetrics>();

    #region Ctor

    public FontAssetCore(FontFamily family, int size, uint resolution)
    {
      _family = family;
      _resolution = resolution;
      _renderedSize = size;
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Returns the font family used by this <see cref="FontAssetCore"/>.
    /// </summary>
    public FontFamily FontFamily
    {
      get { return _family; }
    }

    /// <summary>
    /// Get the size of the font used by this <see cref="FontAssetCore"/> in pixels.
    /// </summary>
    public float Size
    {
      get { return _renderedSize; }
    }

    /// <summary>
    /// Gets the <see cref="FontAssetCore"/>'s ascender for the given font size.
    /// </summary>
    public float Ascender(float fontSize)
    {
      if (!IsAllocated)
        Allocate();
      return _ascender * fontSize / _renderedSize;
    }

    /// <summary>
    /// Gets the height of the <see cref="FontAssetCore"/> if scaled to a different size.
    /// </summary>
    public float LineHeight(float fontSize)
    {
      return fontSize;
    }

    /// <summary>
    /// Gets the width of a string if rendered with this <see cref="FontAssetCore"/> as a particular size.
    /// </summary>
    public float TextWidth(string text, float fontSize, bool kerning)
    {
      return PartialTextWidth(text, 0, text.Length - 1, fontSize, kerning) + CharWidthExtension(text, text.Length - 1, fontSize);
    }

    public float TextWidth(string text, int fromIndex, int toIndex, float fontSize, bool kerning)
    {
      return PartialTextWidth(text, fromIndex, toIndex, fontSize, kerning) + CharWidthExtension(text, toIndex, fontSize);
    }

    public float PartialTextWidth(string text, int fromIndex, int toIndex, float fontSize, bool kerning)
    {
      if (!IsAllocated)
        Allocate();
      float width = 0;
      lock (_syncObj)
        for (int i = fromIndex; i <= toIndex; i++)
          width += Metrics(text[i]).Advance;
      return width * (fontSize / _renderedSize);
    }

    /// <summary>
    /// Gets the width which the glyph of the character at the given index exceeds its advance width.
    /// </summary>
    public float CharWidthExtension(string text, int charIndex, float fontSize)
    {
      if (charIndex < 0 || charIndex >= text.Length)
        return 0.0f;
      if (!IsAllocated)
        Allocate();
      lock (_syncObj)
        return Metrics(text[charIndex]).Overhang * (fontSize / _renderedSize);
    }

    /// <summary>
    /// Gets the height of the glyph of the given character below the baseline, at the rendered size of this font.
    /// </summary>
    public float CharDescent(char character)
    {
      if (!IsAllocated)
        Allocate();
      lock (_syncObj)
        return Metrics(character).Descent;
    }

    public float TextHeight(float fontSize, int lineCount)
    {
      // To center text which is one or more lines in height, we add the value of the font face descender to the expression <lineHeight * lineCount>.
      // The value of descender is <lineHeight - ascender>. Together, it is <lineHeight * (lineCount + 1) - ascender>.
      return LineHeight(fontSize) * (lineCount + 1) - Ascender(fontSize) + 1f;
    }

    #endregion

    #region Font handling

    /// <summary>
    /// Creates the Skia font to draw text with the given <paramref name="fontSize"/>. The returned font must be disposed by the caller.
    /// </summary>
    public SKFont CreateFont(float fontSize)
    {
      KeepAlive();
      return new SKFont(_family.Typeface, fontSize)
        {
          Edging = SKFontEdging.Antialias,
          Subpixel = true,
          Hinting = SKFontHinting.None,
          LinearMetrics = true
        };
    }

    /// <summary>
    /// Creates the font map. Must be called before any metrics are requested.
    /// </summary>
    public void Allocate()
    {
      lock (_syncObj)
      {
        if (IsAllocated)
          return;
        _measureFont = new SKFont(_family.Typeface, _renderedSize)
          {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
            Hinting = SKFontHinting.None,
            LinearMetrics = true
          };
        SKFontMetrics metrics;
        _measureFont.GetFontMetrics(out metrics);
        float height = metrics.Descent - metrics.Ascent + metrics.Leading;
        _ascender = height <= 0 ? _renderedSize * 0.8f : _renderedSize * -metrics.Ascent / height;
      }
      AllocationChanged(AllocationSize);
    }

    // Must be called with the lock held
    private CharMetrics Metrics(char character)
    {
      CharMetrics result;
      if (_charMetrics.TryGetValue(character, out result))
        return result;
      ushort[] glyphs = _family.Typeface.GetGlyphs(character.ToString());
      SKRect bounds;
      float advance = _measureFont.MeasureText(glyphs, out bounds);
      result = new CharMetrics { Advance = advance, Overhang = Math.Max(bounds.Right - advance, 0), Descent = Math.Max(bounds.Bottom, 0) };
      _charMetrics[character] = result;
      return result;
    }

    #endregion

    #region IAssetCore implementation

    public bool IsAllocated
    {
      get
      {
        lock (_syncObj)
          return _measureFont != null;
      }
    }

    public void Free()
    {
      SKFont font;
      lock (_syncObj)
      {
        font = _measureFont;
        _measureFont = null;
        _charMetrics.Clear();
      }
      if (font != null)
      {
        AllocationChanged(-AllocationSize);
        font.Dispose();
      }
    }

    public int AllocationSize
    {
      get { return IsAllocated ? 64 * 1024 : 0; }
    }

    #endregion
  }
}
