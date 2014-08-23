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
using System.Linq;
using System.Xml.Serialization;
using MediaPortal.Utilities;

namespace MediaPortal.Common.MediaManagement.MLQueries
{
  /// <summary>
  /// Filter which filters based on the relationship
  /// </summary>
  public class RelationshipFilter : IFilter
  {
    protected Guid _itemId;
    protected Guid _itemType;
    protected Guid _relationshipType;

    public RelationshipFilter(Guid itemId, Guid itemType, Guid relationshipType)
    {
      _itemId = itemId;
      _itemType = itemType;
      _relationshipType = relationshipType;
    }

    [XmlIgnore]
    public Guid ItemId
    {
      get { return _itemId; }
    }

    [XmlIgnore]
    public Guid ItemType
    {
      get { return _itemType; }
    }

    [XmlIgnore]
    public Guid RelationshipType
    {
      get { return _relationshipType; }
    }

    public override string ToString()
    {
      return "(ITEM_ID='" + _itemId + "' AND ITEM_TYPE='" + _itemType + "' AND RELATIONSHIP_TYPE='" + _relationshipType + "')";
    }

    #region Additional members for the XML serialization

    internal RelationshipFilter() { }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlAttribute("ItemId")]
    public Guid XML_ItemId
    {
      get { return _itemId; }
      set { _itemId = value; }
    }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlAttribute("ItemType")]
    public Guid XML_ItemType
    {
      get { return _itemType; }
      set { _itemType = value; }
    }

    /// <summary>
    /// For internal use of the XML serialization system only.
    /// </summary>
    [XmlAttribute("RelationshipType")]
    public Guid XML_RelationshipType
    {
      get { return _relationshipType; }
      set { _relationshipType = value; }
    }

    #endregion
  }
}
