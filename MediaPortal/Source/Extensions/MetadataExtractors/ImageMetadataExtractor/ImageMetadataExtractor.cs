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

using System.Device.Location;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ThumbnailGenerator;
using MediaPortal.Common.Settings;
using MediaPortal.Extensions.MetadataExtractors.ImageMetadataExtractor.Settings;
using MediaPortal.Extensions.OnlineLibraries;
using MediaPortal.Utilities;
using MediaPortal.Utilities.SystemAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaPortal.Extensions.MetadataExtractors.ImageMetadataExtractor
{
  /// <summary>
  /// MediaPortal 2 metadata extractor implementation for image files. Supports several formats.
  /// </summary>
  public class ImageMetadataExtractor : IMetadataExtractor
  {
    #region Constants

    /// <summary>
    /// GUID string for the image metadata extractor.
    /// </summary>
    public const string METADATAEXTRACTOR_ID_STR = "426AC13A-AC78-4224-A1B1-65CD60AC0B88";

    /// <summary>
    /// Image metadata extractor GUID.
    /// </summary>
    public static Guid METADATAEXTRACTOR_ID = new Guid(METADATAEXTRACTOR_ID_STR);

    /// <summary>
    /// Default mimetype for not detected formats.
    /// </summary>
    public const string DEFAULT_MIMETYPE = "image/unknown";

    #endregion Constants

    #region Protected fields and classes

    protected static ICollection<MediaCategory> MEDIA_CATEGORIES = new List<MediaCategory>();
    protected static ICollection<string> IMAGE_FILE_EXTENSIONS = new List<string>();

    protected MetadataExtractorMetadata _metadata;

    #endregion Protected fields and classes

    #region Ctor

    static ImageMetadataExtractor()
    {
      MEDIA_CATEGORIES.Add(DefaultMediaCategories.Image);
      ImageMetadataExtractorSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<ImageMetadataExtractorSettings>();
      InitializeExtensions(settings);
    }

    /// <summary>
    /// (Re)initializes the movie extensions for which this <see cref="ImageMetadataExtractorSettings"/> used.
    /// </summary>
    /// <param name="settings">Settings object to read the data from.</param>
    internal static void InitializeExtensions(ImageMetadataExtractorSettings settings)
    {
      IMAGE_FILE_EXTENSIONS = new List<string>(settings.ImageFileExtensions.Select(e => e.ToLowerInvariant()));
    }

    public ImageMetadataExtractor()
    {
      _metadata = new MetadataExtractorMetadata(METADATAEXTRACTOR_ID, "Image metadata extractor", MetadataExtractorPriority.Core, true,
          MEDIA_CATEGORIES, new[]
              {
                MediaAspect.Metadata,
                ImageAspect.Metadata
              });
    }

    #endregion Ctor

    #region Protected methods

    /// <summary>
    /// Returns the information if the specified file name (or path) has a file extension which is
    /// supposed to be supported by this metadata extractor.
    /// </summary>
    /// <param name="fileName">Relative or absolute file path to check.</param>
    /// <returns><c>true</c>, if the file's extension is supposed to be supported, else <c>false</c>.</returns>
    protected static bool HasImageExtension(string fileName)
    {
      string ext = DosPathHelper.GetExtension(fileName).ToLowerInvariant();
      return IMAGE_FILE_EXTENSIONS.Contains(ext);
    }

    #endregion Protected methods

    #region IMetadataExtractor implementation

    public MetadataExtractorMetadata Metadata
    {
      get { return _metadata; }
    }

    public bool TryExtractMetadata(IResourceAccessor mediaItemAccessor, IDictionary<Guid, MediaItemAspect> extractedAspectData, bool forceQuickMode)
    {
      string fileName = mediaItemAccessor.ResourceName;
      if (!HasImageExtension(fileName))
        return false;

      MediaItemAspect mediaAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, MediaAspect.Metadata);
      MediaItemAspect imageAspect = MediaItemAspect.GetOrCreateAspect(extractedAspectData, ImageAspect.Metadata);

      try
      {
        if (!(mediaItemAccessor is IFileSystemResourceAccessor))
          return false;
        IFileSystemResourceAccessor fsra = mediaItemAccessor as IFileSystemResourceAccessor;
        // Open a stream for media item to detect mimeType.
        using (Stream mediaStream = fsra.OpenRead())
        {
          string mimeType = MimeTypeDetector.GetMimeType(mediaStream) ?? DEFAULT_MIMETYPE;
          mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE, mimeType);
        }
        // Extract EXIF information from media item.
        using (ExifMetaInfo.ExifMetaInfo exif = new ExifMetaInfo.ExifMetaInfo(fsra))
        {
          mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, ProviderPathHelper.GetFileNameWithoutExtension(fileName));
          mediaAspect.SetAttribute(MediaAspect.ATTR_RECORDINGTIME, exif.OriginalDate != DateTime.MinValue ? exif.OriginalDate : fsra.LastChanged);
          mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT, StringUtils.TrimToNull(exif.ImageDescription));

          if (exif.PixXDim.HasValue) imageAspect.SetAttribute(ImageAspect.ATTR_WIDTH, (int) exif.PixXDim);
          if (exif.PixYDim.HasValue) imageAspect.SetAttribute(ImageAspect.ATTR_HEIGHT, (int) exif.PixYDim);
          imageAspect.SetAttribute(ImageAspect.ATTR_MAKE, StringUtils.TrimToNull(exif.EquipMake));
          imageAspect.SetAttribute(ImageAspect.ATTR_MODEL, StringUtils.TrimToNull(exif.EquipModel));
          if (exif.ExposureBias.HasValue) imageAspect.SetAttribute(ImageAspect.ATTR_EXPOSURE_BIAS, ((double) exif.ExposureBias).ToString());
          imageAspect.SetAttribute(ImageAspect.ATTR_EXPOSURE_TIME, exif.ExposureTime);
          imageAspect.SetAttribute(ImageAspect.ATTR_FLASH_MODE, StringUtils.TrimToNull(exif.FlashMode));
          if (exif.FNumber.HasValue) imageAspect.SetAttribute(ImageAspect.ATTR_FNUMBER, string.Format("F {0}", (double) exif.FNumber));
          imageAspect.SetAttribute(ImageAspect.ATTR_ISO_SPEED, StringUtils.TrimToNull(exif.ISOSpeed));
          imageAspect.SetAttribute(ImageAspect.ATTR_ORIENTATION, (Int32) (exif.OrientationType ?? 0));
          imageAspect.SetAttribute(ImageAspect.ATTR_METERING_MODE, exif.MeteringMode.ToString());

          if (exif.Latitude.HasValue && exif.Longitude.HasValue)
          {
            imageAspect.SetAttribute(ImageAspect.ATTR_LATITUDE, exif.Latitude);
            imageAspect.SetAttribute(ImageAspect.ATTR_LONGITUDE, exif.Longitude);

            CivicAddress locationInfo;
            if (!forceQuickMode && GeoLocationService.Instance.TryLookup(new GeoCoordinate(exif.Latitude.Value, exif.Longitude.Value), out locationInfo))
            {
              imageAspect.SetAttribute(ImageAspect.ATTR_CITY, locationInfo.City);
              imageAspect.SetAttribute(ImageAspect.ATTR_STATE, locationInfo.StateProvince);
              imageAspect.SetAttribute(ImageAspect.ATTR_COUNTRY, locationInfo.CountryRegion);
            }
          }

          using (LocalFsResourceAccessorHelper rah = new LocalFsResourceAccessorHelper(mediaItemAccessor))
          {
            string localFsResourcePath = rah.LocalFsResourceAccessor.LocalFileSystemPath;
            if (localFsResourcePath != null)
            {
              // In quick mode only allow thumbs taken from cache.
              bool cachedOnly = forceQuickMode;

              // Thumbnail extraction
              IThumbnailGenerator generator = ServiceRegistration.Get<IThumbnailGenerator>();
              byte[] thumbData;
              ImageType imageType;
              if (generator.GetThumbnail(localFsResourcePath, 96, 96, cachedOnly, out thumbData, out imageType))
                MediaItemAspect.SetAttribute(extractedAspectData, ThumbnailSmallAspect.ATTR_THUMBNAIL, thumbData);
              if (generator.GetThumbnail(localFsResourcePath, 256, 256, cachedOnly, out thumbData, out imageType))
                MediaItemAspect.SetAttribute(extractedAspectData, ThumbnailLargeAspect.ATTR_THUMBNAIL, thumbData);
            }
          }
        }
        return true;
      }
      catch (Exception e)
      {
        // Only log at the info level here - And simply return false. This makes the importer know that we
        // couldn't perform our task here.
        ServiceRegistration.Get<ILogger>().Info("ImageMetadataExtractor: Exception reading resource '{0}' (Text: '{1}')", mediaItemAccessor.CanonicalLocalResourcePath, e.Message);
      }
      return false;
    }

    #endregion IMetadataExtractor implementation
  }
}