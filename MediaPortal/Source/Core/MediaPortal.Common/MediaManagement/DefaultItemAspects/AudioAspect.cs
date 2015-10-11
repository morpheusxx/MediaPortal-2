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

namespace MediaPortal.Common.MediaManagement.DefaultItemAspects
{
  /// <summary>
  /// Contains the metadata specification of the "Audio" media item aspect which is assigned to all audio media items.
  /// </summary>
  public static class AudioAspect
  {
    /// <summary>
    /// Media item aspect id of the audio aspect.
    /// </summary>
    public static readonly Guid ASPECT_ID = new Guid("493F2B3B-8025-4DB1-80DC-C3CD39683C9F");

    /// <summary>
    /// Enumeration of artist names.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_ARTISTS =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("Artists", 100, Cardinality.ManyToMany, true);

    /// <summary>
    /// Album name.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_ALBUM =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("Album", 100, Cardinality.Inline, true);

    /// <summary>
    /// Enumeration of genre names.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_GENRES =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("Genres", 100, Cardinality.ManyToMany, true);

    /// <summary>
    /// Duration in seconds.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_DURATION =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("Duration", typeof(long), Cardinality.Inline, false);

    /// <summary>
    /// Track number.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_TRACK =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("Track", typeof(int), Cardinality.Inline, false);

    /// <summary>
    /// Number of tracks on the CD.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_NUMTRACKS =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("NumTracks", typeof(int), Cardinality.Inline, false);

    /// <summary>
    /// Enumeration of album artist name strings.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_ALBUMARTISTS =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("AlbumArtists", 100, Cardinality.ManyToMany, true);

    /// <summary>
    /// Enumeration of composer name strings.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_COMPOSERS =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("Composers", 100, Cardinality.ManyToMany, true);

    /// <summary>
    /// Encoding as string. TODO: Describe format.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_ENCODING =
        MediaItemAspectMetadata.CreateSingleStringAttributeSpecification("Encoding", 50, Cardinality.Inline, false);

    /// <summary>
    /// Bitrate in kbits/second.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_BITRATE =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("BitRate", typeof(int), Cardinality.Inline, false);

    /// <summary>
    /// ID of the disc. TODO: Specification.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_DISCID =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("DiscId", typeof(int), Cardinality.Inline, false);

    /// <summary>
    /// Number of discs in the collection.
    /// </summary>
    public static readonly MediaItemAspectMetadata.SingleAttributeSpecification ATTR_NUMDISCS =
        MediaItemAspectMetadata.CreateSingleAttributeSpecification("NumDiscs", typeof(int), Cardinality.Inline, false);

    public static readonly SingleMediaItemAspectMetadata Metadata = new SingleMediaItemAspectMetadata(
        // TODO: Localize name
        ASPECT_ID, "AudioItem", new[] {
            ATTR_ARTISTS,
            ATTR_ALBUM,
            ATTR_GENRES,
            ATTR_DURATION,
            ATTR_TRACK,
            ATTR_NUMTRACKS,
            ATTR_ALBUMARTISTS,
            ATTR_COMPOSERS,
            ATTR_ENCODING,
            ATTR_BITRATE,
            ATTR_DISCID,
            ATTR_NUMDISCS,
        });

      public static readonly Guid ROLE_TRACK = new Guid("10C134B1-4E35-4750-836D-76F3AB58D40A");
  }
}
