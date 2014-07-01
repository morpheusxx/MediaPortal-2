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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPortal.Backend.MediaLibrary;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Extensions.OnlineLibraries;
using MediaPortal.Extensions.UserServices.FanArtService.Interfaces;

namespace MediaPortal.Extensions.MetadataExtractors.MovieMetadataExtractor.FanartProvider
{
  public class MovieFanartProvider : IFanArtProvider
  {
    private static readonly Guid[] NECESSARY_MIAS =
    {
      ProviderResourceAspect.ASPECT_ID,
      MovieAspect.ASPECT_ID,
    };

    /// <summary>
    /// Gets a list of <see cref="FanArtImage"/>s for a requested <paramref name="mediaType"/>, <paramref name="fanArtType"/> and <paramref name="name"/>.
    /// The name can be: Series name, Actor name, Artist name depending on the <paramref name="mediaType"/>.
    /// </summary>
    /// <param name="mediaType">Requested FanArtMediaType</param>
    /// <param name="fanArtType">Requested FanArtType</param>
    /// <param name="name">Requested name of Series, Actor, Artist...</param>
    /// <param name="maxWidth">Maximum width for image. <c>0</c> returns image in original size.</param>
    /// <param name="maxHeight">Maximum height for image. <c>0</c> returns image in original size.</param>
    /// <param name="singleRandom">If <c>true</c> only one random image URI will be returned</param>
    /// <param name="result">Result if return code is <c>true</c>.</param>
    /// <returns><c>true</c> if at least one match was found.</returns>
    public bool TryGetFanArt(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, string name, int maxWidth, int maxHeight, bool singleRandom, out IList<string> result)
    {
      result = null;
      if (mediaType != FanArtConstants.FanArtMediaType.Movie && mediaType != FanArtConstants.FanArtMediaType.MovieCollection)
        return false;

      Guid mediaItemId;
      string[] patterns = null;

      if (Guid.TryParse(name, out mediaItemId) && !GetPattern(mediaType, fanArtType, mediaItemId, out patterns) || patterns == null
        /*|| !GetPattern(mediaType, fanArtType, name, out patterns)*/)
        return false;

      var files = new List<string>();
      foreach (var pattern in patterns)
      {
        try
        {
          var pathPart = Path.GetDirectoryName(pattern);
          var filePart = Path.GetFileName(pattern);
          DirectoryInfo directoryInfo = new DirectoryInfo(pathPart);
          if (directoryInfo.Exists)
          {
            files.AddRange(directoryInfo.GetFiles(filePart).Select(file => file.FullName).ToList());
          }
        }
        catch
        {
        }
      }
      result = files;
      return files.Count > 0;
    }

    protected IList<FanArtImage> GetSingleRandom(IList<FanArtImage> fullList)
    {
      if (fullList.Count <= 1)
        return fullList;

      Random rnd = new Random(DateTime.Now.Millisecond);
      int rndIndex = rnd.Next(fullList.Count - 1);
      return new List<FanArtImage> { fullList[rndIndex] };
    }

    protected bool GetPattern(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, string name, out string[] patterns)
    {
      patterns = null;
      if (mediaType != FanArtConstants.FanArtMediaType.Movie && mediaType != FanArtConstants.FanArtMediaType.MovieCollection)
        return false;

      string basePath = null;
      switch (mediaType)
      {
        case FanArtConstants.FanArtMediaType.Movie:
          int movieDbId;
          basePath = !MovieTheMovieDbMatcher.Instance.TryGetMovieDbId(name, out movieDbId) ? null : Path.Combine(MovieTheMovieDbMatcher.CACHE_PATH, movieDbId.ToString());
          break;
        case FanArtConstants.FanArtMediaType.MovieCollection:
          int collectionId;
          basePath = !MovieTheMovieDbMatcher.Instance.TryGetCollectionId(name, out collectionId) ? null : Path.Combine(MovieTheMovieDbMatcher.CACHE_PATH, "COLL_" + collectionId);
          break;
      }

      return FinishPattern(fanArtType, ref patterns, basePath);
    }

    private static bool FinishPattern(FanArtConstants.FanArtType fanArtType, ref string[] patterns, string basePath)
    {
      if (string.IsNullOrWhiteSpace(basePath))
        return false;

      switch (fanArtType)
      {
        case FanArtConstants.FanArtType.Poster:
          patterns = new[] { Path.Combine(basePath, "Posters\\*.jpg") };
          return true;
        case FanArtConstants.FanArtType.FanArt:
          patterns = new[] { Path.Combine(basePath, "Backdrops\\*.jpg") };
          return true;
      }
      return false;
    }

    protected bool GetPattern(FanArtConstants.FanArtMediaType mediaType, FanArtConstants.FanArtType fanArtType, Guid mediaItemId, out string[] patterns)
    {
      patterns = null;
      IMediaLibrary mediaLibrary = ServiceRegistration.Get<IMediaLibrary>(false);
      if (mediaLibrary == null)
        return false;

      IFilter filter = new MediaItemIdFilter(mediaItemId);
      IList<MediaItem> items = mediaLibrary.Search(new MediaItemQuery(NECESSARY_MIAS, filter), false);
      if (items == null || items.Count == 0)
        return false;

      MediaItem mediaItem = items.First();

      List<string> localPatterns = new List<string>();
      // File based access
      try
      {
        using (var accessor = mediaItem.GetResourceLocator().CreateAccessor())
        {
          ILocalFsResourceAccessor fsra = accessor as ILocalFsResourceAccessor;
          if (fsra != null)
          {
            var fileSystemPath = fsra.LocalFileSystemPath;
            var path = Path.GetDirectoryName(fileSystemPath);
            var file = Path.GetFileNameWithoutExtension(fileSystemPath);

            if (fanArtType == FanArtConstants.FanArtType.Poster)
            {
              localPatterns.Add(Path.ChangeExtension(fileSystemPath, ".jpg"));
              localPatterns.Add(Path.Combine(path, file + "-poster.jpg"));
              localPatterns.Add(Path.Combine(path, "folder.jpg"));
            }
            if (fanArtType == FanArtConstants.FanArtType.FanArt)
            {
              localPatterns.Add(Path.Combine(path, "backdrop.jpg"));
              localPatterns.Add(Path.Combine(path, "ExtraFanArt\\*.jpg"));
            }

            // Copy all patterns for .jpg -> .png
            localPatterns.AddRange(new List<string>(localPatterns).Select(p => p.Replace(".jpg", ".png")));
          }
        }
      }
      catch (Exception)
      {
      }

      string basePath = null;
      int movieDbId;
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_TMDB_ID, out movieDbId))
      {
        switch (mediaType)
        {
          case FanArtConstants.FanArtMediaType.Movie:
            basePath = Path.Combine(MovieTheMovieDbMatcher.CACHE_PATH, movieDbId.ToString());
            break;
        }
      }
      if (FinishPattern(fanArtType, ref patterns, basePath))
      {
        localPatterns.AddRange(patterns);
      }
      patterns = localPatterns.ToArray();
      return localPatterns.Count > 0;
    }
  }
}
