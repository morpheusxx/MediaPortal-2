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
using System.IO;
using SkiaSharp;

namespace MediaPortal.Utilities.Graphics
{
  /// <summary>
  /// Target encoding for images written by <see cref="ImageUtilities"/>.
  /// </summary>
  public enum ImageOutputFormat
  {
    Jpeg,
    Png
  }

  /// <summary>
  /// Portable (SkiaSharp based) image utilities, usable on all platforms. For the System.Drawing based
  /// Windows-only variant see <c>GdiImageUtilities</c> in <c>MediaPortal.Utilities.Windows</c>.
  /// </summary>
  public static class ImageUtilities
  {
    /// <summary>
    /// JPEG quality used when none is given. Matches the default of System.Drawing's JPEG encoder.
    /// </summary>
    public const int DEFAULT_JPEG_QUALITY = 75;

    /// <summary>
    /// Resizes an image that is loaded from the given <paramref name="sourceStream"/> to a maximum <paramref name="maxWidth"/> and
    /// <paramref name="maxHeight"/> while preserving the aspect ratio. Images are never upscaled. The result is encoded as
    /// <paramref name="targetFormat"/>. The returned stream can be read directly from the beginning.
    /// The caller must <see cref="IDisposable.Dispose"/>() the returned instance.
    /// </summary>
    /// <param name="sourceStream">Source stream</param>
    /// <param name="targetFormat">Format to save into target stream</param>
    /// <param name="maxWidth">Max. width</param>
    /// <param name="maxHeight">Max. height</param>
    /// <param name="jpegQuality">Quality (0-100) when <paramref name="targetFormat"/> is <see cref="ImageOutputFormat.Jpeg"/></param>
    /// <returns>Stream containing the resized image, or <c>null</c> if <paramref name="sourceStream"/> is empty</returns>
    /// <exception cref="InvalidDataException">The data could not be decoded as an image.</exception>
    public static Stream ResizeImage(Stream sourceStream, ImageOutputFormat targetFormat, int maxWidth, int maxHeight, int jpegQuality = DEFAULT_JPEG_QUALITY)
    {
      if (sourceStream.CanSeek && sourceStream.Length == 0)
        return null;

      using (SKBitmap source = SKBitmap.Decode(sourceStream))
      {
        if (source == null)
          throw new InvalidDataException("Image data could not be decoded");

        SKSizeI size = FitInto(source.Width, source.Height, maxWidth, maxHeight);
        if (size.Width == source.Width && size.Height == source.Height)
          return Encode(source, targetFormat, jpegQuality);

        using (SKBitmap resized = source.Resize(new SKImageInfo(size.Width, size.Height, source.ColorType, source.AlphaType), SKFilterQuality.High))
        {
          if (resized == null)
            throw new InvalidDataException("Image could not be resized");
          return Encode(resized, targetFormat, jpegQuality);
        }
      }
    }

    /// <summary>
    /// Decodes an image from <paramref name="imageData"/> and saves it as JPEG to <paramref name="path"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The data could not be decoded as an image.</exception>
    public static void SaveAsJpeg(byte[] imageData, string path, int quality = DEFAULT_JPEG_QUALITY)
    {
      using (SKBitmap bitmap = SKBitmap.Decode(imageData))
      {
        if (bitmap == null)
          throw new InvalidDataException("Image data could not be decoded");
        using (Stream encoded = Encode(bitmap, ImageOutputFormat.Jpeg, quality))
        using (FileStream file = File.Create(path))
          encoded.CopyTo(file);
      }
    }

    /// <summary>
    /// Computes the size of an image that is fit into <paramref name="maxWidth"/> x <paramref name="maxHeight"/>
    /// with preserved aspect ratio. Never upscales.
    /// </summary>
    internal static SKSizeI FitInto(int width, int height, int maxWidth, int maxHeight)
    {
      if (width <= maxWidth && height <= maxHeight)
        return new SKSizeI(width, height);

      if (width <= maxWidth)
        maxWidth = width;

      int newHeight = height * maxWidth / width;
      if (newHeight > maxHeight)
      {
        // Resize with height instead
        maxWidth = width * maxHeight / height;
        newHeight = maxHeight;
      }
      return new SKSizeI(Math.Max(1, maxWidth), Math.Max(1, newHeight));
    }

    private static Stream Encode(SKBitmap bitmap, ImageOutputFormat format, int jpegQuality)
    {
      if (jpegQuality < 0 || jpegQuality > 100)
        throw new ArgumentOutOfRangeException(nameof(jpegQuality), jpegQuality, "Jpeg image quality must be between 0 and 100, with 100 being the highest quality.");

      using (SKImage image = SKImage.FromBitmap(bitmap))
      using (SKData data = format == ImageOutputFormat.Png ?
        image.Encode(SKEncodedImageFormat.Png, 100) :
        image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality))
      {
        if (data == null)
          throw new InvalidDataException("Image could not be encoded");
        MemoryStream result = new MemoryStream(data.ToArray());
        result.Position = 0;
        return result;
      }
    }
  }
}
