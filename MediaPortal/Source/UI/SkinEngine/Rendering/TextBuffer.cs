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
using System.Linq;
using SharpDX;
using MediaPortal.UI.SkinEngine.Gpu;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SkiaSharp;
using Size = SharpDX.Size2;
using SizeF = SharpDX.Size2F;
using PointF = SharpDX.Vector2;

namespace MediaPortal.UI.SkinEngine.Rendering
{
  #region Enums
  /// <summary>
  /// An enum used to specify horizontal text-alignment.
  /// </summary>
  public enum HorizontalTextAlignEnum
  {
    /// <summary>
    /// Align text to the left.
    /// </summary>
    Left,
    /// <summary>
    /// Align text to the right.
    /// </summary>
    Right,
    /// <summary>
    /// Cener align text.
    /// </summary>
    Center
  };

  /// <summary>
  /// An enum used to specify vertical text-alignment.
  /// </summary>
  public enum VerticalTextAlignEnum
  {
    /// <summary>
    /// Align text to the top.
    /// </summary>
    Top,
    /// <summary>
    /// Align text to the bottom.
    /// </summary>
    Bottom,
    /// <summary>
    /// Center align text.
    /// </summary>
    Center
  };


  /// <summary>
  /// An enum used to specify text scrolling behaviour.
  /// </summary>
  public enum TextScrollEnum
  {
    /// <summary>
    /// Determine scroll direction based on text size, wrapping and available space.
    /// </summary>
    Auto,

    /// <summary>
    /// No scrolling.
    /// </summary>
    None,

    /// <summary>
    /// Force scrolling text to the left.
    /// </summary>
    Left,

    /// <summary>
    /// Force scrolling text to the right.
    /// </summary>
    Right,

    /// <summary>
    /// Force scrolling text to the top.
    /// </summary>
    Up,

    /// <summary>
    /// Force scrolling text to the bottom.
    /// </summary>
    Down
  };

  /// <summary>
  /// An enum used to specify how text is trimmed when it overflows the edge of its containing box.
  /// </summary>
  public enum TextTrimming
  {
    /// <summary>
    /// Text is not trimmed.
    /// </summary>
    None,
    /// <summary>
    /// Text is trimmed at a character boundary. An ellipsis (...) is drawn in place of remaining text.
    /// </summary>
    CharacterEllipsis,
    /// <summary>
    /// Text is trimmed at a word boundary. An ellipsis (...) is drawn in place of remaining text.
    /// </summary>
    WordEllipsis
  }

  #endregion

  public class TextBuffer : IDisposable
  {
    #region Consts

    protected const int FADE_SIZE = 15;
    protected const string ELLIPSIS = "...";
    #endregion

    #region Protected fields

    // Immutable properties
    protected FontAsset _font;
    protected float _fontSize;
    // State
    protected string _text;
    protected bool _textChanged;
    protected SizeF _lastTextSize;
    protected float _lastTextBoxWidth;
    protected bool _lastWrap;
    protected TextTrimming _lastTextTrimming;
    protected bool _kerning;
    protected string[] _lines;
    protected float[] _lineWidths;
    // Rendering
    EffectAsset _effect;
    // Scrolling
    protected Vector2 _scrollPos;
    protected Vector2 _scrollWrapOffset;
    protected DateTime _lastTimeUsed;
    protected DateTime _scrollInitialized;
    #endregion

    #region Ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBuffer"/> class.
    /// </summary>
    /// <param name="font">The font to use.</param>
    /// <param name="size">The font size (may be slightly different to the size of <paramref name="font"/>).</param>
    public TextBuffer(FontAsset font, float size)
    {
      SetFont(font, size);
      _kerning = true;
      _lastTimeUsed = DateTime.MinValue;
      _lastTextSize = new SizeF();
      ResetScrollPosition();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBuffer"/> class.
    /// </summary>
    /// <param name="fontName">The name of the font to use.</param>
    /// <param name="size">The font size.</param>
    public TextBuffer(string fontName, float size)
    {
      SetFont(fontName, size);
      _kerning = true;
      _lastTimeUsed = DateTime.MinValue;
      _lastTextSize = new SizeF();
      ResetScrollPosition();
    }

    #endregion

    #region Public properties

    /// <summary>
    /// Gets the font.
    /// </summary>
    public FontAsset Font
    {
      get { return _font; }
    }

    /// <summary>
    /// Gets the font size. This may be slightly different from the size stored in Font.
    /// </summary>
    public float FontSize
    {
      get { return _fontSize; }
    }

    /// <summary>
    /// Gets or sets the text to be rendered.
    /// </summary>
    public string Text
    {
      get { return _text; }
      set
      {
        if (_text == value)
          return;
        _textChanged = true;
        ResetScrollPosition();
        _text = value;
      }
    }

    /// <summary>
    /// Gets the current kerning setting.
    /// </summary>
    public bool Kerning
    {
      get { return _kerning; }
    }

    /// <summary>
    /// Gets the width of a string as if it was rendered by this resource.
    /// </summary>
    /// <param name="text">String to evaluate</param>
    /// <returns>The width of the text in graphics device units (pixels)</returns>
    public float TextWidth(string text)
    {
      return _font != null ? _font.TextWidth(text, _fontSize, _kerning) : 0f;
    }

    /// <summary>
    /// Gets the height of a number of text lines if rendered by this resource.
    /// </summary>
    /// <param name="lineCount">The number of lines to measure.</param>
    /// <returns>The height of the text in graphics device units (pixels)</returns>
    public float TextHeight(int lineCount)
    {
      return _font != null ? _font.TextHeight(_fontSize, lineCount) : 0f;
    }

    /// <summary>
    /// Gets the height of text rendered with this resource.
    /// </summary>
    public float LineHeight
    {
      get { return _font != null ? _font.LineHeight(_fontSize) : 0f; }
    }

    public bool IsAllocated
    {
      get { return _lines != null && _font != null && _font.IsAllocated; }
    }

    public override string ToString()
    {
      return Text;
    }

    #endregion

    #region Public methods
    /// <summary>
    /// Sets the <see cref="FontAsset"/> for this buffer.
    /// </summary>
    /// <param name="font">The <see cref="FontAsset"/>to use.</param>
    /// <param name="size">The size to use for the font.</param>
    public void SetFont(FontAsset font, float size)
    {
      DisposeFont();
      if (_font != null)
        _font.Deallocated -= DisposeBuffer;
      _font = font;
      _font.Deallocated += DisposeBuffer;
      _fontSize = size;
      _textChanged = true;
    }
    /// <summary>
    /// Sets the <see cref="FontAsset"/> for this buffer.
    /// </summary>
    /// <param name="fontName">The name of the font to use.</param>
    /// <param name="size">The size to use for the font.</param>
    public void SetFont(String fontName, float size)
    {
      SetFont(ContentManager.Instance.GetFont(fontName, size), size);
    }

    public string[] GetLines(float maxWidth, float maxHeight, bool wrap, TextTrimming textTrimming)
    {
      string[] lines = wrap ? WrapText(maxWidth) : _text.Split(Environment.NewLine.ToCharArray());
      if (textTrimming != TextTrimming.None)
        lines = TrimText(lines, textTrimming, maxWidth, maxHeight);
      return lines;
    }

    /// <summary>
    /// Allocates or re-allocates this resource.
    /// </summary>
    /// <param name="boxWidth"></param>
    /// <param name="boxHeight"></param>
    /// <param name="wrap"></param>
    /// <param name="textTrimming"></param>
    public void Allocate(float boxWidth, float boxHeight, bool wrap, TextTrimming textTrimming)
    {
      if (String.IsNullOrEmpty(_text))
      {
        DisposeBuffer();
        return;
      }
      if (_font == null)
        return;

      // Layout the text lines
      string[] lines = GetLines(boxWidth, boxHeight, wrap, textTrimming);
      LayoutLines(lines);

      // Preserve state
      _lastTextBoxWidth = boxWidth;
      _lastWrap = wrap;
      _lastTextTrimming = textTrimming;
      _textChanged = false;
    }

    /// <summary>
    /// Wraps the text of this label to the specified <paramref name="maxWidth"/> and returns the wrapped
    /// text parts. Wrapping is performed on a word-by-word basis.
    /// </summary>
    /// <param name="maxWidth">Maximum available width before the text should be wrapped.</param>
    /// <returns>An array of strings holding the wrapped text lines.</returns>
    public string[] WrapText(float maxWidth)
    {
      if (string.IsNullOrEmpty(_text))
        return new string[0];

      // Remove trailing whitespaces, because wrap logic depends on non-whitespaces for last line.
      var text = _text.TrimEnd();
      IList<string> result = new List<string>();
      foreach (string para in text.Split(Environment.NewLine.ToCharArray()))
      {
        int paraLength = para.Length;
        int nextIndex = 0;
        float lineWidth = 0;
        int lineStartIndex = 0; // Start index of the current line to be examined

        // Split paragraphs into lines that will fit into maxWidth
        while (nextIndex < paraLength)
        {
          int sectionIndex = nextIndex;
          // Iterate over leading spaces
          while (nextIndex < paraLength && char.IsWhiteSpace(para[nextIndex]))
            ++nextIndex;
          int lastIndex = nextIndex; // Remember index to avoid busy loops if not a single character fits
          if (nextIndex < paraLength)
          {
            // Find length of next word
            int wordIndex = nextIndex;
            while (nextIndex < paraLength && !char.IsWhiteSpace(para[nextIndex]))
              ++nextIndex;
            // Does the word fit into the space?
            // Remember to take into account the additional width required if this was the last word on the line.
            float cx = _font.PartialTextWidth(para, sectionIndex, nextIndex - 1, _fontSize, Kerning);
            float extension = _font.CharWidthExtension(para, nextIndex - 1, _fontSize);

            if (lineWidth + cx + extension > maxWidth)
            {
              // If the word is longer than a line width wrap it by letter
              if (cx + extension > maxWidth)
              {
                // Double check without leading whitespace
                if (_font.PartialTextWidth(para, wordIndex, nextIndex - 1, _fontSize, Kerning) + extension > maxWidth)
                {
                  // Don't wrap whitespace
                  while (sectionIndex < para.Length && char.IsWhiteSpace(para[sectionIndex]))
                  {
                    lineWidth += _font.TextWidth(para, sectionIndex, sectionIndex, _fontSize, Kerning);
                    ++sectionIndex;
                  }
                  // See how many characters of the word we can fit on this line
                  lineWidth += _font.TextWidth(para, sectionIndex, sectionIndex, _fontSize, Kerning);
                  while (lineWidth < maxWidth)
                  {
                    lineWidth += _font.TextWidth(para, sectionIndex, sectionIndex, _fontSize, Kerning);
                    ++sectionIndex;
                  }
                  // Prepare for next line
                  wordIndex = sectionIndex;
                  nextIndex = wordIndex;
                }
              }
              // Start new line
              if (sectionIndex != lineStartIndex)
                result.Add(para.Substring(lineStartIndex, sectionIndex - lineStartIndex));
              lineStartIndex = wordIndex;
              lineWidth = _font.TextWidth(para, wordIndex, nextIndex - 1, _fontSize, Kerning);
            }
            else
              lineWidth += cx;
            if (nextIndex >= paraLength)
            {
              // End of paragraphs
              result.Add(para.Substring(lineStartIndex, nextIndex - lineStartIndex));
              lineStartIndex = nextIndex;
            }
          }
          if (nextIndex == lastIndex)
            break;
        }
        // If no words found add an empty line to preserve text formatting
        if (lineStartIndex == 0)
          result.Add("");
      }
      return result.ToArray();
    }

    /// <summary>
    /// Trims any text that doesn't fit in the available space and draws an ellipsis (...) in place of any remaining text.
    /// </summary>
    /// <param name="lines">Text to trim</param>
    /// <param name="textTrimming">Whether to trim on a word or character boundary.</param>
    /// <param name="maxWidth">Maximum available width before the text should be trimmed.</param>
    /// <param name="maxHeight">Maximum available height before the text should be trimmed.</param>
    /// <returns></returns>
    public string[] TrimText(string[] lines, TextTrimming textTrimming, float maxWidth, float maxHeight)
    {
      if (textTrimming == TextTrimming.None || lines.Length == 0)
        return lines;

      //calculate maximum visible lines
      int maxLines = (int)(maxHeight / _font.LineHeight(_fontSize));
      if (maxLines < 1)
        return lines;

      int trimmedLineCount = Math.Min(maxLines, lines.Length);
      string[] trimmedLines = new string[trimmedLineCount];

      float ellipsisWidth = _font.TextWidth(ELLIPSIS, _fontSize, _kerning);
      for (int i = 0; i < trimmedLineCount; i++)
      {
        string line = lines[i];
        float textWidth = _font.TextWidth(line, _fontSize, _kerning);
        if (textWidth <= maxWidth && (i < trimmedLineCount - 1 || trimmedLineCount == lines.Length))
        {
          //line fits and we don't need to indicate that lines have been trimmed
          trimmedLines[i] = line;
          continue;
        }

        int lastIndex = 0;
        if (textWidth + ellipsisWidth <= maxWidth)
          //line fits but we need to show an ellipsis because we are trimming lines
          lastIndex = line.Length;
        else
        {
          int nextIndex = lastIndex;
          float lineWidth = 0;
          while (nextIndex < line.Length)
          {
            if (textTrimming == TextTrimming.WordEllipsis)
            {
              //find word boundary
              while (nextIndex < line.Length && char.IsWhiteSpace(line[nextIndex]))
                ++nextIndex;
              while (nextIndex < line.Length && !char.IsWhiteSpace(line[nextIndex]))
                ++nextIndex;
            }
            else
              //character boundary
              ++nextIndex;

            //see if substring will fit after ellipsis is added
            float cx = _font.PartialTextWidth(line, lastIndex, nextIndex - 1, _fontSize, _kerning);
            if (lineWidth + cx + ellipsisWidth <= maxWidth)
            {
              lastIndex = nextIndex;
              lineWidth += cx;
            }
            else
              //substring won't fit
              break;
          }
        }
        //trim line and add ellipsis
        string trimmedLine = lastIndex < line.Length ? line.Remove(lastIndex) : line;
        trimmedLines[i] = trimmedLine + ELLIPSIS;
      }
      return trimmedLines;
    }

    /// <summary>
    /// Standard draw method for this text.
    /// </summary>
    /// <param name="textBox">The box where the text should be drawn.</param>
    /// <param name="horzAlignment">The horizontal alignment.</param>
    /// <param name="vertAlignment">The vertical alignment.</param>
    /// <param name="color">The color.</param>
    /// <param name="wrap">If <c>true</c> then text will be word-wrapped to fit the <paramref name="textBox"/>.</param>
    /// <param name="fade">If <c>true</c> then text will be faded at the edge of the <paramref name="textBox"/>.</param>
    /// <param name="zOrder">A value indicating the depth (and thus position in the visual heirachy) that this element should be rendered at.</param>
    /// <param name="scrollMode">Text scrolling behaviour.</param>
    /// <param name="scrollSpeed">Text scrolling speed in units (pixels at original skin size) per second.</param>
    /// <param name="scrollDelay">Text scrolling delay in seconds.</param>
    /// <param name="finalTransform">The final combined layout/render-transform.</param>
    public void Render(RectangleF textBox, HorizontalTextAlignEnum horzAlignment, VerticalTextAlignEnum vertAlignment, Color4 color,
        bool wrap, TextTrimming textTrimming, bool fade, float zOrder, TextScrollEnum scrollMode, float scrollSpeed, float scrollDelay, Matrix finalTransform)
    {
      if (!IsAllocated || wrap != _lastWrap || _lastTextTrimming != textTrimming || _textChanged || ((wrap || textTrimming != TextTrimming.None) && textBox.Width != _lastTextBoxWidth))
      {
        Allocate(textBox.Width, textBox.Height, wrap, textTrimming);
        if (!IsAllocated)
          return;
      }

      // Update scrolling
      TextScrollEnum actualScrollMode = scrollMode;
      if (scrollMode != TextScrollEnum.None && _lastTimeUsed != DateTime.MinValue)
        actualScrollMode = UpdateScrollPosition(textBox, scrollMode, scrollSpeed, scrollDelay);

      // Do vertical alignment by adjusting yPosition
      float yPosition = 0.0f;
      switch (vertAlignment)
      {
        case VerticalTextAlignEnum.Bottom:
          yPosition = Math.Max(textBox.Height - _lastTextSize.Height, 0.0f);
          break;
        case VerticalTextAlignEnum.Center:
          yPosition += Math.Max((textBox.Height - _lastTextSize.Height) / 2.0f, 0.0f);
          break;
        //case TextAlignEnum.Top:
        // Do nothing
      }

      // Do we need to add fading edges?
      Vector4? fadeBorder = null;
      Vector4 border;
      if (fade && CalculateFadeBorder(actualScrollMode, textBox, horzAlignment, out border))
        fadeBorder = border;

      // Render
      DoRender(textBox, horzAlignment, _scrollPos.X, _scrollPos.Y + yPosition, color, fadeBorder, finalTransform);

      // Because text wraps around before it is complete scrolled off the screen we may need to render a second copy
      // to create the desired wrapping effect
      if (scrollMode != TextScrollEnum.None)
      {
        if (!float.IsNaN(_scrollWrapOffset.X))
          DoRender(textBox, horzAlignment, _scrollPos.X + _scrollWrapOffset.X, _scrollPos.Y, color, fadeBorder, finalTransform);
        else if (!float.IsNaN(_scrollWrapOffset.Y))
          DoRender(textBox, horzAlignment, _scrollPos.X, _scrollPos.Y + _scrollWrapOffset.Y, color, fadeBorder, finalTransform);
      }
      _lastTimeUsed = SkinContext.FrameRenderingStartTime;
    }

    /// <summary>
    /// Simplified render method to draw the text with a given text offset. This can be used for text edit controls where the text is
    /// shifted horizontally against its textbox.
    /// </summary>
    /// <param name="textBox">The box where the text should be drawn.</param>
    /// <param name="horzAlignment">The horizontal alignment.</param>
    /// <param name="vertAlignment">The vertical alignment.</param>
    /// <param name="offsetX">Horizontal offset of the text in relation to its text box. A negative offset will make the text start left of its
    /// normal position.</param>
    /// <param name="color">The color.</param>
    /// <param name="zOrder">A value indicating the depth (and thus position in the visual heirachy) that this element should be rendered at.</param>
    /// <param name="finalTransform">The final combined layout/render-transform.</param>
    public void Render(RectangleF textBox, HorizontalTextAlignEnum horzAlignment, VerticalTextAlignEnum vertAlignment, float offsetX,
        Color4 color, float zOrder, Matrix finalTransform)
    {
      if (!IsAllocated || _lastWrap || _lastTextTrimming != TextTrimming.None || _textChanged)
      {
        Allocate(textBox.Width, textBox.Height, false, TextTrimming.None);
        if (!IsAllocated)
          return;
      }

      // Do vertical alignment by adjusting yPosition
      float yPosition = 0.0f;
      switch (vertAlignment)
      {
        case VerticalTextAlignEnum.Bottom:
          yPosition = Math.Max(textBox.Height - _lastTextSize.Height, 0.0f);
          break;
        case VerticalTextAlignEnum.Center:
          yPosition += Math.Max((textBox.Height - _lastTextSize.Height) / 2.0f, 0.0f);
          break;
        //case TextAlignEnum.Top:
        // Do nothing
      }

      // Render without fading
      DoRender(textBox, horzAlignment, offsetX, yPosition, color, null, finalTransform);
      _lastTimeUsed = SkinContext.FrameRenderingStartTime;
    }

    #endregion

    #region Protected methods

    /// <summary>
    /// Calculates the widths of the given text lines and the size of the text.
    /// </summary>
    protected void LayoutLines(string[] lines)
    {
      _lines = lines;
      _lineWidths = new float[lines.Length];
      float maxWidth = 0;
      for (int i = 0; i < lines.Length; i++)
      {
        string line = lines[i];
        float width = line.Length == 0 ? 0f : _font.PartialTextWidth(line, 0, line.Length - 1, _fontSize, _kerning);
        _lineWidths[i] = width;
        maxWidth = Math.Max(maxWidth, width);
      }
      // Every line is _fontSize high. The first baseline is _fontSize below the top of the text. The text ends at the descent of the last
      // glyph of the last line, plus a little padding.
      float scale = _fontSize / _font.Size;
      char lastChar = ' ';
      for (int i = lines.Length - 1; i >= 0; i--)
      {
        string line = lines[i].TrimEnd();
        if (line.Length > 0)
        {
          lastChar = line[line.Length - 1];
          break;
        }
      }
      float height = _fontSize * lines.Length + (_font.CharDescent(lastChar) + 2) * scale;
      _lastTextSize = new SizeF(maxWidth, height);
    }

    protected void DoRender(RectangleF textBox, HorizontalTextAlignEnum horzAlignment, float scrollX, float scrollY, Color4 color,
        Vector4? fadeBorder, Matrix finalTransform)
    {
      SKCanvas canvas = GraphicsDevice.Canvas;
      if (canvas == null || _lines == null || _font == null)
        return;
      SKRect clip = SKRect.Create(textBox.Left, textBox.Top, textBox.Width, textBox.Height);
      canvas.Save();
      try
      {
        canvas.SetMatrix(GraphicsDevice.ToSkMatrix(finalTransform));
        canvas.ClipRect(clip);
        if (fadeBorder.HasValue)
          canvas.SaveLayer(clip, null);
        using (SKFont font = _font.CreateFont(_fontSize))
        using (SKPaint paint = new SKPaint())
        {
          paint.IsAntialias = true;
          paint.Color = new SKColor((byte) (color.Red * 255 + 0.5f), (byte) (color.Green * 255 + 0.5f), (byte) (color.Blue * 255 + 0.5f), (byte) (color.Alpha * 255 + 0.5f));
          for (int i = 0; i < _lines.Length; i++)
          {
            string line = _lines[i];
            if (line.Length == 0)
              continue;
            float x;
            switch (horzAlignment)
            {
              case HorizontalTextAlignEnum.Center:
                x = textBox.Width / 2.0f - 0.5f * _lineWidths[i];
                break;
              case HorizontalTextAlignEnum.Right:
                x = textBox.Width - _lineWidths[i];
                break;
              default:
                x = 0.0f;
                break;
            }
            float baseline = _fontSize * (i + 1);
            canvas.DrawText(line, textBox.Left + x + scrollX, textBox.Top + scrollY + baseline, font, paint);
          }
        }
        if (fadeBorder.HasValue)
        {
          ApplyFade(canvas, clip, fadeBorder.Value);
          canvas.Restore();
        }
      }
      finally
      {
        canvas.Restore();
      }
      GraphicsDevice.RenderTargetChanged();
    }

    /// <summary>
    /// Fades out the content of the current layer at the borders of the given <paramref name="box"/>.
    /// </summary>
    /// <param name="canvas">Canvas with a layer to modify.</param>
    /// <param name="box">The text box.</param>
    /// <param name="border">Width of the fading area at the left, top, right and bottom border. Values near zero mean no fading.</param>
    private static void ApplyFade(SKCanvas canvas, SKRect box, Vector4 border)
    {
      using (SKPaint paint = new SKPaint())
      {
        paint.BlendMode = SKBlendMode.DstIn;
        FadeEdge(canvas, paint, border.X, new SKRect(box.Left, box.Top, box.Left + border.X, box.Bottom), new SKPoint(box.Left, 0), new SKPoint(box.Left + border.X, 0));
        FadeEdge(canvas, paint, border.Z, new SKRect(box.Right - border.Z, box.Top, box.Right, box.Bottom), new SKPoint(box.Right, 0), new SKPoint(box.Right - border.Z, 0));
        FadeEdge(canvas, paint, border.Y, new SKRect(box.Left, box.Top, box.Right, box.Top + border.Y), new SKPoint(0, box.Top), new SKPoint(0, box.Top + border.Y));
        FadeEdge(canvas, paint, border.W, new SKRect(box.Left, box.Bottom - border.W, box.Right, box.Bottom), new SKPoint(0, box.Bottom), new SKPoint(0, box.Bottom - border.W));
      }
    }

    private static void FadeEdge(SKCanvas canvas, SKPaint paint, float size, SKRect area, SKPoint from, SKPoint to)
    {
      if (size < 1f)
        return;
      using (SKShader shader = SKShader.CreateLinearGradient(from, to, new[] { SKColors.Transparent, SKColors.White }, SKShaderTileMode.Clamp))
      {
        paint.Shader = shader;
        canvas.DrawRect(area, paint);
      }
      paint.Shader = null;
    }

    protected TextScrollEnum UpdateScrollPosition(RectangleF textBox, TextScrollEnum mode, float speed, float scrollDelay)
    {
      if ((SkinContext.FrameRenderingStartTime - _scrollInitialized).TotalSeconds < scrollDelay)
        return TextScrollEnum.None;

      float dif = speed * (float) SkinContext.FrameRenderingStartTime.Subtract(_lastTimeUsed).TotalSeconds;

      if (mode == TextScrollEnum.Auto)
      {
        if (_lastWrap && _lastTextSize.Height > textBox.Height)
          mode = TextScrollEnum.Up;
        else if (_lines.Length == 1 && _lastTextSize.Width > textBox.Width)
          mode = TextScrollEnum.Left;
        else
          return TextScrollEnum.None;
      }

      switch (mode)
      {
        case TextScrollEnum.Left:
          _scrollPos.X -= dif;
          if (_scrollPos.X + _lastTextSize.Width < textBox.Width / 2.0f)
          {
            _scrollWrapOffset.X = _scrollPos.X;
            _scrollPos.X = textBox.Width + 4;
            _scrollWrapOffset.X -= _scrollPos.X;
          }
          else if (_scrollWrapOffset.X + _scrollPos.X + _lastTextSize.Width < 0.0f)
            _scrollWrapOffset.X = float.NaN;
          break;
        case TextScrollEnum.Right:
          _scrollPos.X += dif;
          if (_scrollPos.X > textBox.Width / 2.0f)
          {
            _scrollWrapOffset.X = _scrollPos.X;
            _scrollPos.X = -_lastTextSize.Width - 4;
            _scrollWrapOffset.X -= _scrollPos.X;
          }
          else if (_scrollWrapOffset.X + _scrollPos.X > textBox.Width)
            _scrollWrapOffset.X = float.NaN;
          break;
        case TextScrollEnum.Down:
          _scrollPos.Y += dif;
          if (_scrollPos.Y > textBox.Height / 2.0f)
          {
            _scrollWrapOffset.Y = _scrollPos.Y;
            _scrollPos.Y = -_lastTextSize.Height - 4;
            _scrollWrapOffset.Y -= _scrollPos.Y;
          }
          else if (_scrollWrapOffset.Y + _scrollPos.Y > textBox.Height)
            _scrollWrapOffset.Y = float.NaN;
          break;
        //case TextScrollEnum.Up:
        default:
          _scrollPos.Y -= dif;
          if (_scrollPos.Y + _lastTextSize.Height < textBox.Height / 2.0f)
          {
            _scrollWrapOffset.Y = _scrollPos.Y;
            _scrollPos.Y = textBox.Height + 4;
            _scrollWrapOffset.Y -= _scrollPos.Y;
          }
          else if (_scrollWrapOffset.Y + _scrollPos.Y + _lastTextSize.Height < 0.0f)
            _scrollWrapOffset.Y = float.NaN;
          break;
      }
      return mode;
    }

    public void ResetScrollPosition()
    {
      _scrollPos = new Vector2(0.0f, 0.0f);
      _scrollWrapOffset = new Vector2(float.NaN, float.NaN);
      _scrollInitialized = DateTime.Now;
    }

    #endregion

    #region Protected methods

    protected bool CalculateFadeBorder(TextScrollEnum scrollMode, RectangleF textBox, HorizontalTextAlignEnum horzAlign, out Vector4 fadeBorder)
    {
      fadeBorder = new Vector4(0.0001f, 0.0001f, 0.0001f, 0.0001f);
      bool dofade = false;
      if (scrollMode == TextScrollEnum.Left || scrollMode == TextScrollEnum.Right)
      {
        fadeBorder.X = FADE_SIZE; // Fade on left edge
        fadeBorder.Z = FADE_SIZE; // Fade on right edge
        dofade = true;
      }
      else if (_lastTextSize.Width > textBox.Width)
      {
        if (horzAlign == HorizontalTextAlignEnum.Right || horzAlign == HorizontalTextAlignEnum.Center)
          fadeBorder.X = FADE_SIZE; // Fade on left edge
        if (horzAlign == HorizontalTextAlignEnum.Left || horzAlign == HorizontalTextAlignEnum.Center)
          fadeBorder.Z = FADE_SIZE; // Fade on right edge
        dofade = true;
      }
      if (scrollMode == TextScrollEnum.Up || scrollMode == TextScrollEnum.Down)
      {
        fadeBorder.Y = FADE_SIZE; // Fade on top edge
        fadeBorder.W = FADE_SIZE; // Fade on bottom edge
        dofade = true;
      }
      else if (_lastTextSize.Height > textBox.Height + _fontSize / 4)
      {
        fadeBorder.W = FADE_SIZE; // Fade on bottom edge
        dofade = true;
      }
      return dofade;
    }

    void DisposeBuffer()
    {
      _lines = null;
      _lineWidths = null;
    }

    void DisposeFont()
    {
      if (_font != null)
        _font.Deallocated -= DisposeBuffer;
      _font = null;
    }

    #endregion

    #region IDisposable implementation

    public void Dispose()
    {
      DisposeBuffer();
      DisposeFont();
    }

    #endregion
  }
}
