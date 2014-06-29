﻿#region Copyright (C) 2007-2014 Team MediaPortal

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.ResourceAccess;

namespace MediaPortal.Common.Services.MediaManagement.ImportDataflowBlocks
{
  /// <summary>
  /// Takes directory MediaItems and saves them to the MediaLibrary
  /// </summary>
  class DirectorySaveBlock : ImporterWorkerDataflowBlockBase
  {
    #region Consts

    public const String BLOCK_NAME = "DirectorySaveBlock";
    private static readonly IEnumerable<Guid> DIRECTORY_MIA_ID_ENUMERATION = new[]
      {
        DirectoryAspect.ASPECT_ID
      };
    private static readonly IEnumerable<Guid> EMPTY_MIA_ID_ENUMERATION = new Guid[] { };

    #endregion

    #region Variables

    private readonly bool _refresh;
    private readonly ConcurrentDictionary<ResourcePath, Guid> _parentDirectoryIds = new ConcurrentDictionary<ResourcePath, Guid>();

    #endregion

    #region Constructor

    /// <summary>
    /// Initiates the DirectoryUnfoldBlock
    /// </summary>
    /// <param name="ct">CancellationToken used to cancel this block</param>
    /// <param name="refresh"><c>true</c> if this is a refresh import, otherwise <c>false</c></param>
    /// <param name="parentImportJobController">ImportJobController to which this DirectoryUnfoldBlock belongs</param>
    public DirectorySaveBlock(CancellationToken ct, bool refresh, ImportJobController parentImportJobController) : base(
      new ExecutionDataflowBlockOptions { CancellationToken = ct },
      new ExecutionDataflowBlockOptions { CancellationToken = ct },
      new ExecutionDataflowBlockOptions { CancellationToken = ct },
      BLOCK_NAME, true, parentImportJobController)
    {
      _refresh = refresh;
    }

    #endregion

    #region Private methods

    /// <summary>
    /// Main method that is called for every <see cref="PendingImportResourceNewGen"/> in this block
    /// </summary>
    /// <param name="importResource">Directory resource to be saved to the MediaLibrary</param>
    /// <returns></returns>
    private async Task<PendingImportResourceNewGen> ProcessDirectory(PendingImportResourceNewGen importResource)
    {
      try
      {
        // Directories that are single resources (such as DVD directories) are not saved in this block
        // We just pass them to the next block.
        if (!importResource.IsSingleResource)
        {
          // We only save to the MediaLibrary if
          // (a) this is a first time import (i.e. not a refresh import), or
          // (b) this is a refresh import and the respective directory MediaItem is not yet in the MediaLibrary
          if (!_refresh || await IsRefreshNeeded(importResource.ResourceAccessor))
          {
            var parentDirectoryId = await GetParentDirectoryId(importResource.ParentDirectory);
            if (parentDirectoryId == null)
            {
              // If we cannot determine the parent directory ID we have an error case and
              // cannot save this directory MediaItem
              importResource.IsValid = false;
              return importResource;
            }
            var directoryId = await AddDirectory(importResource.ResourceAccessor, parentDirectoryId.Value);
            _parentDirectoryIds[importResource.PendingResourcePath] = directoryId;
          }
        }

        // ToDo: Remove this and do it later
        importResource.IsValid = false;

        return importResource;
      }
      catch (TaskCanceledException)
      {
        return importResource;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Warn("ImporterWorker.{0}.{1}: Error while processing {2}", ex, ParentImportJobController, ToString(), importResource);
        importResource.IsValid = false;
        return importResource;
      }
    }

    /// <summary>
    /// Checks, in case of refresh-imports, whether a refresh of a given directory is necessary
    /// </summary>
    /// <param name="directoryAccessor">ResourceAccessor to the directory to be checked</param>
    /// <returns></returns>
    private async Task<bool> IsRefreshNeeded(IFileSystemResourceAccessor directoryAccessor)
    {
      var directoryPath = directoryAccessor.CanonicalLocalResourcePath;
      var directoryItem = await LoadLocalItem(directoryPath, EMPTY_MIA_ID_ENUMERATION, DIRECTORY_MIA_ID_ENUMERATION);
      if (directoryItem != null)
      {
        MediaItemAspect directoryAspect;
        if (!directoryItem.Aspects.TryGetValue(DirectoryAspect.ASPECT_ID, out directoryAspect))
        {
          // This is the case if the parentResourcePath was formerly imported as a single resource.
          // We cannot reuse it and it is necessary to delete this old MediaItem.
          await DeleteMediaItem(directoryPath);
          directoryItem = null;
        }
        else
          // This directory is already correctly stored in the MediaLibrary. No need to store it again,
          // we just cache its ID for potential subdirectories to be stored during this refresh.
          _parentDirectoryIds[directoryPath] = directoryItem.MediaItemId;
      }
      return (directoryItem == null);
    }
    
    /// <summary>
    /// Determines the MediaItemId of a given MediaItem's parent directory
    /// </summary>
    /// <param name="parentResourcePath">Path to the MediaItem for which the parent directory ID is requested</param>
    /// <returns>MediaItemId of the parent directory or <c>null</c> if it cannot be determined</returns>
    private async Task<Guid?> GetParentDirectoryId(ResourcePath parentResourcePath)
    {
      // Parent directory of a share's BasePath is null and must be saved
      // with Guid.Empty as parent directory ID
      if (parentResourcePath == null)
        return Guid.Empty;

      // We save directories in the order we unfolded them, i.e. the parent directory has
      // been saved before we try to save the child directory. When saving the parent directory
      // we store its ID in _parentDirectoryIds to cache them. So usually we should find the
      // parent directory ID in this cache.
      Guid result;
      if (_parentDirectoryIds.TryGetValue(parentResourcePath, out result))
        return result;

      // If the above wasn't successful, we have to load the parent directory MediaItem from
      // the MediaLibrary to get its ID. This should only be necessary if the ImportJob was
      // persisted to disk before and resumed after a restart of the application. In this
      // case we don't have the parent directory IDs cached in _parentDirectoryIds.
      var parentDirectoryMediaItem = await LoadLocalItem(parentResourcePath, DIRECTORY_MIA_ID_ENUMERATION, EMPTY_MIA_ID_ENUMERATION);
      if (parentDirectoryMediaItem == null)
      {
        // If the parent directory ID could not be found in the MediaLibrary, this is an error
        // case: The order of the directories to be saved was wrong.
        ServiceRegistration.Get<ILogger>().Error("ImporterWorker.{0}.{1}: Could not find GUID of parent directory ({2}). Directories were posted to this block in the wrong order.", ParentImportJobController, ToString(), parentResourcePath);
        return null;
      }
      // If we had to load the parent directory ID from the MediaLibrary, we store it in our
      // cache so that we don't have to load it again for the next subdirectory of that
      // parent directory.
      _parentDirectoryIds[parentResourcePath] = parentDirectoryMediaItem.MediaItemId;
      return parentDirectoryMediaItem.MediaItemId;
    }

    /// <summary>
    /// Adds a direcotry MediaItem to the MediaLibrary
    /// </summary>
    /// <param name="directoryAccessor">ResourceAccessor to the directory to be saved</param>
    /// <param name="parentDirectoryId">ID of the parent Directory</param>
    /// <returns></returns>
    private async Task<Guid> AddDirectory(IFileSystemResourceAccessor directoryAccessor, Guid parentDirectoryId)
    {
      var directoryPath = directoryAccessor.CanonicalLocalResourcePath;
      var mediaAspect = new MediaItemAspect(MediaAspect.Metadata);
      mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, directoryAccessor.ResourceName);
      mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE, null);
      mediaAspect.SetAttribute(MediaAspect.ATTR_RECORDINGTIME, DateTime.MinValue);
      mediaAspect.SetAttribute(MediaAspect.ATTR_RATING, 0);
      mediaAspect.SetAttribute(MediaAspect.ATTR_COMMENT, null);
      mediaAspect.SetAttribute(MediaAspect.ATTR_LASTPLAYED, DateTime.MinValue);
      var directoryAspect = new MediaItemAspect(DirectoryAspect.Metadata);
      IList<MediaItemAspect> aspects = new List<MediaItemAspect>(new[]
        {
            mediaAspect,
            directoryAspect
        });
      return await UpdateMediaItem(parentDirectoryId, directoryPath, aspects);
    }

    #endregion

    #region Base overrides

    protected override IPropagatorBlock<PendingImportResourceNewGen, PendingImportResourceNewGen> CreateInnerBlock()
    {
      return new TransformBlock<PendingImportResourceNewGen, PendingImportResourceNewGen>(new Func<PendingImportResourceNewGen, Task<PendingImportResourceNewGen>>(ProcessDirectory), InnerBlockOptions);
    }

    #endregion
  }
}
