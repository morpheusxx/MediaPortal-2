﻿using System.Runtime.Serialization;

namespace MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.DataStructures
{
  /// <summary>
  /// Data Structure for Movie Items going to Trakt
  /// </summary>
  [DataContract]
  public class TraktMovieScrobble : AbstractScrobble
  {
    [DataMember(Name = "imdb_id")]
    public string IMDBID { get; set; }

    [DataMember(Name = "tmdb_id")]
    public string TMDBID { get; set; }
  }
}
