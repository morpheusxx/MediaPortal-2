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
using System.Collections.Generic;
using System.IO;
using ManagedBass;

namespace MediaPortal.Extensions.BassLibraries
{
  /// <summary>
  /// Tags and basic properties of an audio file, read with BASS.
  /// </summary>
  public class AudioFileTags
  {
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public string AlbumArtist { get; set; }
    public string Composer { get; set; }
    public string Genre { get; set; }
    public string Comment { get; set; }
    public string Year { get; set; }

    /// <summary>
    /// Track number, without the total number of tracks ("3/12" results in "3").
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Bitrate in kbit/s.
    /// </summary>
    public int Bitrate { get; set; }

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public double Duration { get; set; }

    private static string Get(IDictionary<string, string> frames, string id)
    {
      string value;
      return frames.TryGetValue(id, out value) ? value : null;
    }

    private static string RemoveTotal(string track)
    {
      if (string.IsNullOrEmpty(track))
        return track;
      int index = track.IndexOf('/');
      return index > 0 ? track.Substring(0, index) : track;
    }

    /// <summary>
    /// Reads the tags and properties of the given audio file.
    /// </summary>
    /// <param name="fileName">Local file name.</param>
    /// <returns>Tags or <c>null</c>, if the file cannot be read by BASS.</returns>
    public static AudioFileTags Read(string fileName)
    {
      // Make sure that the native libraries are found and BASS is initialized (the "no sound" device is sufficient to decode)
      BassUtils.EnsureInitialized();

      int handle = Bass.CreateStream(fileName, 0, 0, BassFlags.Decode);
      if (handle == BassConstants.BassInvalidHandle)
        return null;
      try
      {
        AudioFileTags result = new AudioFileTags();
        // The ID3v2 frames are decoded here, the tag reader of ManagedBass is used for the other tag formats (APE, OGG, MP4, ...)
        IDictionary<string, string> id3 = Id3v2Reader.ReadTextFrames(Bass.ChannelGetTags(handle, TagType.ID3v2));
        if (id3.Count > 0)
        {
          result.Title = Get(id3, "TIT2");
          result.Artist = Get(id3, "TPE1");
          result.Album = Get(id3, "TALB");
          result.AlbumArtist = Get(id3, "TPE2");
          result.Composer = Get(id3, "TCOM");
          result.Genre = Get(id3, "TCON");
          result.Comment = Get(id3, "COMM");
          string year = Get(id3, "TDRC") ?? Get(id3, "TYER");
          result.Year = year != null && year.Length >= 4 ? year.Substring(0, 4) : year;
          result.Track = RemoveTotal(Get(id3, "TRCK"));
        }
        else
        {
          TagReader reader = TagReader.Read(handle);
          if (reader != null)
          {
            result.Title = reader.Title;
            result.Artist = reader.Artist;
            result.Album = reader.Album;
            result.AlbumArtist = reader.AlbumArtist;
            result.Composer = reader.Composer;
            result.Genre = reader.Genre;
            result.Comment = reader.Comment;
            result.Year = reader.Year;
            result.Track = RemoveTotal(reader.Track);
          }
        }

        long length = Bass.ChannelGetLength(handle);
        if (length > 0)
          result.Duration = Bass.ChannelBytes2Seconds(handle, length);
        float bitrate;
        if (Bass.ChannelGetAttribute(handle, ChannelAttribute.Bitrate, out bitrate) && bitrate > 0)
          result.Bitrate = (int) Math.Round(bitrate);
        else if (result.Duration > 0)
        {
          try
          {
            result.Bitrate = (int) (new FileInfo(fileName).Length * 8 / result.Duration / 1000);
          }
          catch (IOException)
          { }
        }
        return result;
      }
      finally
      {
        Bass.StreamFree(handle);
      }
    }
  }
}
