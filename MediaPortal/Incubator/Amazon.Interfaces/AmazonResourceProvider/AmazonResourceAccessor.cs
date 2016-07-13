#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaPortal.Common.ResourceAccess;
using OnlineVideos.Sites.JSurf.Properties;

namespace Amazon.Interfaces.AmazonResourceProvider
{
  /// <summary>
  /// A specialized <see cref="INetworkResourceAccessor"/> that communicates with Amazon Prime.
  /// Bound to the <see cref="AmazonResourceProvider"/>.
  /// </summary>
  [DebuggerDisplay("AmazonResourceAccessor: '{_descriptor.Caption}' Path: {_descriptor.Path};")]
  public class AmazonResourceAccessor : IFileSystemResourceAccessor
  {
    protected OnlineDescriptor _descriptor;

    [DebuggerDisplay("Path: {Path}, '{Caption}'")]
    public class OnlineDescriptor
    {
      public string Caption;
      public string Url;
      public string Path;
    }

    internal const string ROOT_PROVIDER_PATH = "/";

    protected static ICollection<OnlineDescriptor> DescriptorsList = new List<OnlineDescriptor>
    {
      new OnlineDescriptor { Caption = "[Amazon.Watchlist]", Path = "/W" }, // Watchlist
      new OnlineDescriptor { Caption = "[Amazon.WatchlistMovie]", Path = "/W/M", Url = Resources.AmazonMovieWatchlistUrl }, // Watchlist Movies
      new OnlineDescriptor { Caption = "[Amazon.WatchlistTV]", Path = "/W/T", Url = Resources.AmazonTVWatchlistUrl }, // Watchlist TV
      new OnlineDescriptor { Caption = "[Amazon.Movies]", Path = "/M", Url = Resources.AmazonMovieCategoriesUrl }, // Movies
      new OnlineDescriptor { Caption = "[Amazon.TV]", Path = "/T", Url = Resources.AmazonTVCategoriesUrl }, // TV
      new OnlineDescriptor { Caption = "[Amazon.MoviesEditorsPick]", Path = "/M/ME", Url = Resources.AmazonMovieEditorsUrl }, // Editor's Picks
      new OnlineDescriptor { Caption = "[Amazon.MoviesRecentlyAdded]", Path = "/M/MA", Url = Resources.AmazonMovieRecentUrl }, // Recently Added
      new OnlineDescriptor { Caption = "[Amazon.MoviesPopular]", Path = "/M/MP", Url = Resources.AmazonMoviePopularUrl }, // Popular Movies
      new OnlineDescriptor { Caption = "[Amazon.TVEditorsPick]", Path = "/T/TE", Url = Resources.AmazonMovieEditorsUrl }, // Editor's Picks
      new OnlineDescriptor { Caption = "[Amazon.TVRecentlyAdded]", Path = "/T/TA", Url = Resources.AmazonMovieRecentUrl }, // Recently Added
      new OnlineDescriptor { Caption = "[Amazon.TVPopular]", Path = "/T/TP", Url = Resources.AmazonMoviePopularUrl }, // Popular TV Shows
    };

    protected static Dictionary<string, OnlineDescriptor> PathMappings;

    static AmazonResourceAccessor()
    {
      PathMappings = DescriptorsList.ToDictionary(d => d.Path);
    }

    public AmazonResourceAccessor(string path)
    {
      if (path != ROOT_PROVIDER_PATH)
      {
        var resourcePath = ResourcePath.Deserialize(path);
        path = resourcePath.LastPathSegment.Path;
      }
      if (!PathMappings.TryGetValue(path, out _descriptor))
        _descriptor = new OnlineDescriptor { Path = path };
    }
    public AmazonResourceAccessor(OnlineDescriptor descriptor)
    {
      _descriptor = descriptor;
    }

    public ResourcePath CanonicalLocalResourcePath
    {
      get { return ResourcePath.BuildBaseProviderPath(AmazonResourceProvider.AMAZON_RESOURCE_PROVIDER_ID, AmazonResourceProvider.ToProviderResourcePath(_descriptor.Path).Serialize()); }
    }

    public IResourceAccessor Clone()
    {
      return new AmazonResourceAccessor(_descriptor);
    }

    public IResourceProvider ParentProvider
    {
      get { return null; }
    }

    public string Path
    {
      get { return ResourcePath.BuildBaseProviderPath(AmazonResourceProvider.AMAZON_RESOURCE_PROVIDER_ID, _descriptor.Path).Serialize(); }
    }

    public string ResourceName
    {
      get { return _descriptor.Caption ?? _descriptor.Path.Split('/').Last(); }
    }

    public string ResourcePathName
    {
      get { return _descriptor.Path; }
    }

    public void Dispose()
    {
    }

    public bool Exists { get { return true; } }
    public bool IsFile { get; private set; }
    public DateTime LastChanged { get; private set; }
    public long Size { get; private set; }
    public bool ResourceExists(string path)
    {
      return true;
    }

    public IFileSystemResourceAccessor GetResource(string path)
    {
      return null;
    }

    public ICollection<IFileSystemResourceAccessor> GetFiles()
    {
      return new List<IFileSystemResourceAccessor>();
    }

    public ICollection<IFileSystemResourceAccessor> GetChildDirectories()
    {
      List<IFileSystemResourceAccessor> result = new List<IFileSystemResourceAccessor>();

      // List sub categories
      result.AddRange(PathMappings.Keys.Where(k => _descriptor.Path == GetParent(k)).Select(subPath => new AmazonResourceAccessor(PathMappings[subPath])));
      return result;
    }

    public string GetParent(string virtualPath)
    {
      if (string.IsNullOrEmpty(virtualPath) || virtualPath == "/")
        return "/";

      var parts = virtualPath.Split('/');
      var result = string.Join("/", parts.Take(parts.Length - 1).ToArray());
      return string.IsNullOrEmpty(result) ? "/" : result;
    }

    public void PrepareStreamAccess()
    {
    }

    public Stream OpenRead()
    {
      return null;
    }

    public Task<Stream> OpenReadAsync()
    {
      return null;
    }

    public Stream OpenWrite()
    {
      return null;
    }

    public static bool IsResource(string path)
    {
      return true;
    }
  }
}

