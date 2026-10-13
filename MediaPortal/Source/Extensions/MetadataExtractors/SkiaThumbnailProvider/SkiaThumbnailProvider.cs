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
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.Services.ThumbnailGenerator;
using MediaPortal.Utilities.Graphics;

namespace MediaPortal.Extensions.MetadataExtractors.SkiaThumbnailProvider
{
  /// <summary>
  /// Platform independent thumbnail provider for images, based on SkiaSharp. It runs after the Windows specific providers
  /// (higher priority value) and is the only image provider on other platforms.
  /// </summary>
  public class SkiaThumbnailProvider : IThumbnailProvider
  {
    public bool GetThumbnail(string fileName, int width, int height, bool cachedOnly, out byte[] thumbnailBinary, out ImageType imageType)
    {
      try
      {
        using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
          return GetThumbnail(fileStream, width, height, cachedOnly, out thumbnailBinary, out imageType);
      }
      catch
      {
        thumbnailBinary = null;
        imageType = ImageType.Unknown;
        return false;
      }
    }

    public bool GetThumbnail(Stream stream, int width, int height, bool cachedOnly, out byte[] imageData, out ImageType imageType)
    {
      imageData = null;
      imageType = ImageType.Unknown;
      // No support for cache
      if (cachedOnly)
        return false;

      try
      {
        ImageOutputFormat format;
        if (!ImageUtilities.TryCreateThumbnail(stream, width, height, out imageData, out format))
          return false;
        imageType = format == ImageOutputFormat.Png ? ImageType.Png : ImageType.Jpeg;
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Warn("SkiaThumbnailProvider: Error creating thumbnail from image data", ex);
        return false;
      }
    }
  }
}
