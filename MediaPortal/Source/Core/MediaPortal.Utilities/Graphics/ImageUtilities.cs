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
    /// Creates a thumbnail from the image in <paramref name="sourceStream"/>: the EXIF orientation is applied, the image is
    /// scaled down to fit <paramref name="maxWidth"/> x <paramref name="maxHeight"/> (never upscaled) and encoded as PNG if the
    /// source has an alpha channel, otherwise as JPEG.
    /// </summary>
    /// <param name="sourceStream">Source stream</param>
    /// <param name="maxWidth">Max. width</param>
    /// <param name="maxHeight">Max. height</param>
    /// <param name="imageData">Encoded thumbnail</param>
    /// <param name="format">Encoding of <paramref name="imageData"/></param>
    /// <param name="jpegQuality">Quality (0-100) used for JPEG output</param>
    /// <returns><c>true</c> if the image could be decoded and the thumbnail was created</returns>
    public static bool TryCreateThumbnail(Stream sourceStream, int maxWidth, int maxHeight, out byte[] imageData, out ImageOutputFormat format, int jpegQuality = 95)
    {
      imageData = null;
      format = ImageOutputFormat.Jpeg;
      using (SKCodec codec = SKCodec.Create(sourceStream))
      {
        if (codec == null)
          return false;
        bool hasAlpha = codec.Info.AlphaType != SKAlphaType.Opaque;
        SKEncodedOrigin origin = codec.EncodedOrigin;
        using (SKBitmap decoded = SKBitmap.Decode(codec))
        {
          if (decoded == null)
            return false;
          using (SKBitmap oriented = ApplyOrigin(decoded, origin))
          {
            SKSizeI size = FitInto(oriented.Width, oriented.Height, maxWidth, maxHeight);
            format = hasAlpha ? ImageOutputFormat.Png : ImageOutputFormat.Jpeg;
            if (size.Width == oriented.Width && size.Height == oriented.Height)
            {
              using (Stream encoded = Encode(oriented, format, jpegQuality))
                imageData = ((MemoryStream)encoded).ToArray();
              return true;
            }
            using (SKBitmap resized = oriented.Resize(new SKImageInfo(size.Width, size.Height, oriented.ColorType, oriented.AlphaType), SKFilterQuality.High))
            {
              if (resized == null)
                return false;
              using (Stream encoded = Encode(resized, format, jpegQuality))
                imageData = ((MemoryStream)encoded).ToArray();
              return true;
            }
          }
        }
      }
    }

    /// <summary>
    /// Returns a bitmap in which the EXIF <paramref name="origin"/> (orientation) of <paramref name="source"/> is applied.
    /// If no transformation is needed, <paramref name="source"/> itself is returned (the caller must not dispose twice,
    /// <see cref="SKBitmap.Dispose"/> is idempotent).
    /// </summary>
    internal static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
      int w = source.Width;
      int h = source.Height;
      SKMatrix m;
      switch (origin)
      {
        case SKEncodedOrigin.TopRight: // mirror horizontal
          m = new SKMatrix { ScaleX = -1, TransX = w, ScaleY = 1, Persp2 = 1 };
          break;
        case SKEncodedOrigin.BottomRight: // rotate 180
          m = new SKMatrix { ScaleX = -1, TransX = w, ScaleY = -1, TransY = h, Persp2 = 1 };
          break;
        case SKEncodedOrigin.BottomLeft: // mirror vertical
          m = new SKMatrix { ScaleX = 1, ScaleY = -1, TransY = h, Persp2 = 1 };
          break;
        case SKEncodedOrigin.LeftTop: // transpose
          m = new SKMatrix { SkewX = 1, SkewY = 1, Persp2 = 1 };
          break;
        case SKEncodedOrigin.RightTop: // rotate 90 clockwise
          m = new SKMatrix { SkewX = -1, TransX = h, SkewY = 1, Persp2 = 1 };
          break;
        case SKEncodedOrigin.RightBottom: // transverse
          m = new SKMatrix { SkewX = -1, TransX = h, SkewY = -1, TransY = w, Persp2 = 1 };
          break;
        case SKEncodedOrigin.LeftBottom: // rotate 270 clockwise
          m = new SKMatrix { SkewX = 1, SkewY = -1, TransY = w, Persp2 = 1 };
          break;
        default:
          return source;
      }
      bool swap = origin >= SKEncodedOrigin.LeftTop;
      SKBitmap result = new SKBitmap(swap ? h : w, swap ? w : h, source.ColorType, source.AlphaType);
      using (SKCanvas canvas = new SKCanvas(result))
      {
        canvas.SetMatrix(m);
        canvas.DrawBitmap(source, 0, 0);
      }
      return result;
    }

    /// <summary>
    /// Decodes the image in <paramref name="sourceStream"/> into a premultiplied BGRA bitmap, applies the EXIF orientation and scales it
    /// down to fit <paramref name="maxWidth"/> x <paramref name="maxHeight"/> (never upscaled). The result is suited to be uploaded to the GPU.
    /// </summary>
    /// <returns>The bitmap, or <c>null</c> if the data could not be decoded.</returns>
    public static SKBitmap LoadBitmap(Stream sourceStream, int maxWidth, int maxHeight)
    {
      using (SKCodec codec = SKCodec.Create(sourceStream))
      {
        if (codec == null)
          return null;
        SKEncodedOrigin origin = codec.EncodedOrigin;
        SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        SKBitmap decoded = new SKBitmap(info);
        SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
          decoded.Dispose();
          return null;
        }
        SKBitmap oriented = ApplyOrigin(decoded, origin);
        if (!ReferenceEquals(oriented, decoded))
          decoded.Dispose();

        SKSizeI size = FitInto(oriented.Width, oriented.Height, maxWidth, maxHeight);
        if (size.Width == oriented.Width && size.Height == oriented.Height)
          return oriented;
        SKBitmap resized = oriented.Resize(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul), SKFilterQuality.High);
        oriented.Dispose();
        return resized;
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
