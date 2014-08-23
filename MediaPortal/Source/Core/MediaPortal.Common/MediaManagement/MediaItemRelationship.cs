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
using System.Xml;
using UPnP.Infrastructure.Utils;

namespace MediaPortal.Common.MediaManagement
{
  public class MediaItemRelationship
  {
    #region Constants

    public static readonly string ELEMENT_NAME = "Relationship";

    #endregion

    #region Protected fields

    protected readonly Guid _itemType;

    protected readonly Guid _relationshipType;
    protected readonly Guid _relationshipId;

    #endregion

    public MediaItemRelationship(Guid itemType, Guid relationshipType, Guid relationshipId)
    {
      this._itemType = itemType;
      this._relationshipType = relationshipType;
      this._relationshipId = relationshipId;
    }

    public Guid ItemType
    {
      get { return _itemType; }
    }

    public Guid RelationshipType
    {
      get { return _relationshipType; }
    }

    public Guid RelationshipId
    {
      get { return _relationshipId; }
    }

    public void Serialize(XmlWriter writer)
    {
      writer.WriteStartElement(ELEMENT_NAME);
      writer.WriteAttributeString("ItemType", _itemType.ToString());
      writer.WriteAttributeString("RelationshipType", _relationshipType.ToString());
      writer.WriteAttributeString("RelationshipId", _relationshipId.ToString());
      writer.WriteEndElement(); // ELEMENT_NAME
    }

    public static MediaItemRelationship Deserialize(XmlReader reader)
    {
      //Console.WriteLine("MediaItemRelationship entry, {0} {1}", reader.NodeType, reader.Name);

      if (!reader.MoveToAttribute("ItemType"))
        throw new ArgumentException("Media item relationship cannot be deserialized: 'ItemType' attribute missing");
      Guid itemType = new Guid(reader.ReadContentAsString());

      if (!reader.MoveToAttribute("RelationshipType"))
        throw new ArgumentException("Media item relationship cannot be deserialized: 'RelationshipType' attribute missing");
      Guid relationshipType = new Guid(reader.ReadContentAsString());

      if (!reader.MoveToAttribute("RelationshipId"))
        throw new ArgumentException("Media item relationship cannot be deserialized: 'RelationshipId' attribute missing");
      Guid relationshipId = new Guid(reader.ReadContentAsString());

      MediaItemRelationship result = new MediaItemRelationship(itemType, relationshipType, relationshipId);

      reader.MoveToElement(); // ELEMENT_NAME

      if (!SoapHelper.ReadEmptyStartElement(reader, ELEMENT_NAME))
        throw new ArgumentException("Media item relationship cannot be deserialized: Unable to read empty start element");

      //Console.WriteLine("MediaItemRelationship exit, {0} {1}", reader.NodeType, reader.Name);
      return result;
    }
  }
}
