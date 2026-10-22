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
using ManagedBass;

namespace MediaPortal.UI.Players.BassPlayer
{
  /// <summary>
  /// Tags (title, artist, ...) of a BASS stream, for example the current title of a web radio.
  /// </summary>
  public class TagInfo
  {
    public TagInfo(string fileName)
    {
      FileName = fileName;
    }

    public string FileName { get; private set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public string AlbumArtist { get; set; }
    public string Genre { get; set; }
    public string Comment { get; set; }
    public string Year { get; set; }
    public string Track { get; set; }

    /// <summary>
    /// Bitrate in kbit/s, <c>0</c> if unknown.
    /// </summary>
    public int Bitrate { get; set; }

    /// <summary>
    /// Updates the tags from the metadata of the given BASS stream: the embedded tags (ID3, OGG, ...) and, for web streams,
    /// the ICY headers (station name, genre, bitrate) and the stream title which is sent inline.
    /// </summary>
    public void UpdateFromStream(int handle)
    {
      TagReader reader = TagReader.Read(handle);
      if (reader != null)
      {
        Title = Coalesce(reader.Title, Title);
        Artist = Coalesce(reader.Artist, Artist);
        Album = Coalesce(reader.Album, Album);
        AlbumArtist = Coalesce(reader.AlbumArtist, AlbumArtist);
        Genre = Coalesce(reader.Genre, Genre);
        Comment = Coalesce(reader.Comment, Comment);
        Year = Coalesce(reader.Year, Year);
        Track = Coalesce(reader.Track, Track);
      }

      // Headers of the web server (Shoutcast/Icecast)
      foreach (string line in ReadStrings(handle, TagType.ICY, false) ?? ReadStrings(handle, TagType.HTTP, false) ?? new string[0])
      {
        int index = line.IndexOf(':');
        if (index <= 0)
          continue;
        string key = line.Substring(0, index).Trim().ToLowerInvariant();
        string value = line.Substring(index + 1).Trim();
        switch (key)
        {
          case "icy-name":
            Album = Coalesce(Album, value);
            break;
          case "icy-genre":
            Genre = Coalesce(Genre, value);
            break;
          case "icy-br":
            int bitrate;
            if (int.TryParse(value, out bitrate))
              Bitrate = bitrate;
            break;
        }
      }

      // Title of the currently playing song: StreamTitle='Artist - Title';StreamUrl='...';
      string[] meta = ReadStrings(handle, TagType.META, true);
      if (meta != null && meta.Length > 0)
      {
        string streamTitle = ExtractField(meta[0], "StreamTitle");
        if (!string.IsNullOrEmpty(streamTitle))
        {
          int separator = streamTitle.IndexOf(" - ", StringComparison.Ordinal);
          if (separator > 0)
          {
            Artist = streamTitle.Substring(0, separator).Trim();
            Title = streamTitle.Substring(separator + 3).Trim();
          }
          else
            Title = streamTitle;
        }
      }
    }

    private static string Coalesce(string preferred, string fallback)
    {
      return string.IsNullOrEmpty(preferred) ? fallback : preferred;
    }

    private static string[] ReadStrings(int handle, TagType type, bool singleString)
    {
      IntPtr pointer = Bass.ChannelGetTags(handle, type);
      if (pointer == IntPtr.Zero)
        return null;
      return singleString ? new[] { ManagedBass.Extensions.PtrToStringUtf8(pointer) } : ManagedBass.Extensions.ExtractMultiStringUtf8(pointer);
    }

    private static string ExtractField(string meta, string field)
    {
      string start = field + "='";
      int index = meta.IndexOf(start, StringComparison.OrdinalIgnoreCase);
      if (index < 0)
        return null;
      index += start.Length;
      int end = meta.IndexOf("';", index, StringComparison.Ordinal);
      if (end < 0)
        end = meta.LastIndexOf('\'');
      return end > index ? meta.Substring(index, end - index) : null;
    }

    public override string ToString()
    {
      return string.Format("{0} - {1}", Artist, Title);
    }
  }
}
