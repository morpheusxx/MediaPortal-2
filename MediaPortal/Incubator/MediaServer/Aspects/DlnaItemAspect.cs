﻿#region Copyright (C) 2007-2012 Team MediaPortal

/*
    Copyright (C) 2007-2012 Team MediaPortal
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
using MediaPortal.Common.MediaManagement;

namespace MediaPortal.Extensions.MediaServer.Aspects
{
  public class DlnaItemAspect
  {
    /// <summary>
    /// Media item aspect id of the recording aspect.
    /// </summary>
    public static readonly Guid ASPECT_ID = new Guid("49623e7a-1fdd-4ead-a830-e794f417a1d4");

    /// <summary>
    /// Contains the mime type of the media item.
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_MIME_TYPE =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("MimeType", 50, Cardinality.Inline, false);

    /// <summary>
    /// Contains the recording start date and time.
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_PROFILE =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("Profile", 50, Cardinality.Inline, false);

    /// <summary>
    /// Contains the recording start date and time.
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_SIZE =
        MediaItemAspectMetadata.CreateAttributeSpecification("Size", typeof(Int64), Cardinality.Inline, false);


 public static readonly MediaItemAspectMetadata Metadata = new MediaItemAspectMetadata(
        ASPECT_ID, "DlnaItem", new[] {
            ATTR_MIME_TYPE,
            ATTR_PROFILE,
            ATTR_SIZE
        });
  }
}
