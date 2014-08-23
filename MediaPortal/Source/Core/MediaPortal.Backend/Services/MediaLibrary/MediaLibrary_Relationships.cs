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

using MediaPortal.Common.MediaManagement;
using System;

namespace MediaPortal.Backend.Services.MediaLibrary
{
  public class MediaLibrary_Relationships
  {
    /// <summary>
    /// Media item aspect id of the relationship aspect.
    /// </summary>
    public static readonly Guid ASPECT_ID = new Guid("B87189F6-2251-4A56-B520-262C028A8516");

    /// <summary>
    /// ID on the left side of the relationship
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_LEFT_ID =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("LeftId", 40, Cardinality.Inline, false);

    /// <summary>
    /// Type on the left side of the relationship
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_LEFT_TYPE =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("LeftType", 40, Cardinality.Inline, false);

    /// <summary>
    /// ID on the right side of the relationship
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_RIGHT_ID =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("RightId", 40, Cardinality.Inline, false);

    /// <summary>
    /// Type on the right side of the relationship
    /// </summary>
    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_RIGHT_TYPE =
        MediaItemAspectMetadata.CreateStringAttributeSpecification("RightType", 40, Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata Metadata = new MediaItemAspectMetadata(
      // TODO: Localize name
        ASPECT_ID, "Relationships", new[] {
            ATTR_LEFT_ID,
            ATTR_LEFT_TYPE,
            ATTR_RIGHT_ID,
            ATTR_RIGHT_TYPE
        });
  }
}
