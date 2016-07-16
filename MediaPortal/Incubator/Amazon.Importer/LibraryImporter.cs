using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using MediaPortal.Backend.MediaLibrary;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.SystemResolver;
using OnlineVideos.MediaPortal2.Interfaces.Metadata;
using OnlineVideos.MediaPortal2.ResourceAccess;

namespace Amazon.Importer
{
  public class LibraryImporter
  {
    public void ImportSqlite()
    {
      try
      {
        var dt = ReadData();
        ImportToMediaLibrary(dt);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("Error: ", ex);
      }
    }

    private void ImportToMediaLibrary(DataTable dt)
    {
      IMediaLibrary ml = ServiceRegistration.Get<IMediaLibrary>();
      ISystemResolver sr = ServiceRegistration.Get<ISystemResolver>();
      Guid parentDirectory = Guid.Empty;
      var systemId = sr.LocalSystemId;

      Guid SHARE_ID = new Guid("{E7CAEABE-79F5-46D7-91CF-5CC5A0FBFC13}");
      var ROOT_PATH = RawTokenResourceProvider.ToProviderResourcePath("");

      var share = ml.GetShare(SHARE_ID);
      if (share == null)
      {
        share = new Share(SHARE_ID, systemId, ROOT_PATH, "Amazon Prime Movies", new List<string> { "Video", "Movie" });
        ml.RegisterShare(share);
      }

      MediaItemAspect directoryAspect = new MediaItemAspect(DirectoryAspect.Metadata);
      parentDirectory = ml.AddOrUpdateMediaItem(parentDirectory, systemId, ROOT_PATH, new[] { directoryAspect });

      Dictionary<Guid, MediaItemAspect> aspects = new Dictionary<Guid, MediaItemAspect>();

      foreach (DataRow row in dt.Rows)
      {
        aspects.Clear();
        MediaItemAspect mediaAspect = new MediaItemAspect(MediaAspect.Metadata);
        MediaItemAspect videoAspect = new MediaItemAspect(VideoAspect.Metadata);
        MediaItemAspect movieAspect = new MediaItemAspect(MovieAspect.Metadata);
        MediaItemAspect onlineVideosAspect = new MediaItemAspect(OnlineVideosAspect.Metadata);
        aspects.Add(MediaAspect.ASPECT_ID, mediaAspect);
        aspects.Add(VideoAspect.ASPECT_ID, videoAspect);
        aspects.Add(MovieAspect.ASPECT_ID, movieAspect);
        aspects.Add(OnlineVideosAspect.ASPECT_ID, onlineVideosAspect);

        string asin = (row["asin"] as string ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (asin == null)
          continue;

        ResourcePath path = RawTokenResourceProvider.ToProviderResourcePath(asin);

        mediaAspect.SetAttribute(MediaAspect.ATTR_MIME_TYPE, "video/onlinebrowser");
        mediaAspect.SetAttribute(MediaAspect.ATTR_TITLE, row["movietitle"] as string);


        videoAspect.SetAttribute(VideoAspect.ATTR_STORYPLOT, row["plot"] as string);
        int value;
        if (TryCast(row, "audio", out value))
          videoAspect.SetAttribute(VideoAspect.ATTR_AUDIOSTREAMCOUNT, value);

        MovieInfo movie = new MovieInfo();
        movie.MovieName = row["movietitle"] as string;
        movie.Certification = row["mpaa"] as string;

        ICollection<string> values;
        if (TrySplit(row, "director", out values))
          movie.Directors.AddRange(values);

        if (TrySplit(row, "writer", out values))
          movie.Writers.AddRange(values);

        if (TrySplit(row, "genres", out values, '/'))
          movie.Genres.AddRange(values);

        if (TryCast(row, "year", out value))
          movie.Year = value;

        if (TryCast(row, "stars", out value))
          movie.TotalRating = value;

        if (TryCast(row, "votes", out value))
          movie.RatingCount = value;

        if (TryCast(row, "runtime", out value))
          movie.Runtime = value;

        if (TrySplit(row, "actors", out values))
          movie.Actors.AddRange(values);

        movie.SetMetadata(aspects);

        // TODO: support other siteutils
        onlineVideosAspect.SetAttribute(OnlineVideosAspect.ATTR_SITEUTIL, "Amazon Prime De");
        string uri;
        if (TryParseUrl(row ,"fanart", out uri))
          onlineVideosAspect.SetAttribute(OnlineVideosAspect.ATTR_FANART, uri);
        if (TryParseUrl(row, "poster", out uri))
          onlineVideosAspect.SetAttribute(OnlineVideosAspect.ATTR_POSTER, uri);

        ml.AddOrUpdateMediaItem(parentDirectory, systemId, path, aspects.Values);
      }
    }

    protected bool TryCast<TE>(DataRow row, string colName, out TE value)
    {
      if (row[colName] == DBNull.Value)
      {
        value = default(TE);
        return false;
      }
      value = (TE)Convert.ChangeType(row[colName], typeof(TE));
      return value != null;
    }

    protected bool TryParseUrl(DataRow row, string colName, out string uri)
    {
      Uri result;
      if (Uri.TryCreate(row[colName] as string, UriKind.Absolute, out result))
      {
        uri = result.ToString();
        return true;
      }
      uri = null;
      return false;
    }

    protected bool TrySplit(DataRow row, string colName, out ICollection<string> values, char delimiter = ',')
    {
      values = (row[colName] as string ?? string.Empty).Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).Select(val => val.Trim()).ToList();
      return values.Count > 0;
    }

    private DataTable ReadData()
    {
      var connBuilder = new SQLiteConnectionStringBuilder();
      connBuilder.FullUri = @"D:\Coding\MP\MP2\tests\AmazonPrime\movies.db";

      SQLiteConnection connection = new SQLiteConnection(connBuilder.ToString());
      connection.Open();

      var cmd = connection.CreateCommand();
      cmd.CommandText = "select * from movies order by asin";
      DataTable dt = new DataTable();
      using (SQLiteDataAdapter da = new SQLiteDataAdapter(cmd))
        da.Fill(dt);

      connection.Close();
      return dt;
    }
  }
}
