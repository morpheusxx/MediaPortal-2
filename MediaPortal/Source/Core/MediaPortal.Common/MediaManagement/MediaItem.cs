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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess;

namespace MediaPortal.Common.MediaManagement
{
  /// <summary>
  /// Instances of this class are used for holding the data for single entries in media item views.
  /// </summary>
  /// <remarks>
  /// Instances of this class contain multiple media item aspect instances but not necessarily all media item
  /// aspects of the underlaying media item from the media library are contained.
  /// </remarks>
  public class MediaItem : IEquatable<MediaItem>, IXmlSerializable
  {
    #region Protected fields

    protected Guid _id;
    protected readonly IDictionary<Guid, IList<MediaItemAspect>> _aspects;

    #endregion

    /// <summary>
    /// Creates a new media item.
    /// </summary>
    /// <param name="mediaItemId">Id of the media item in the media library. For local media items, this must be <c>Guid.Empty</c>.</param>
    public MediaItem(Guid mediaItemId) : this()
    {
      _id = mediaItemId;
    }

    /// <summary>
    /// Creates a new media item.
    /// </summary>
    /// <param name="mediaItemId">Id of the media item in the media library. For local media items, this must be <c>Guid.Empty</c>.</param>
    /// <param name="aspects">Dictionary of media item aspects for the new media item instance.</param>
    public MediaItem(Guid mediaItemId, IDictionary<Guid, IList<MediaItemAspect>> aspects)
    {
      _id = mediaItemId;
      _aspects = new Dictionary<Guid, IList<MediaItemAspect>>(aspects);
      if (!_aspects.ContainsKey(ProviderResourceAspect.ASPECT_ID))
        throw new ArgumentException(string.Format("Media items always have to contain the '{0}' aspect",
            typeof(ProviderResourceAspect).Name));
    }

    public Guid MediaItemId
    {
      get { return _id; }
    }

    public IDictionary<Guid, IList<MediaItemAspect>> Aspects
    {
      get { return _aspects; }
    }

    /// <summary>
    /// Returns the media item aspect of the specified <paramref name="mediaItemAspectId"/>, if it is
    /// contained in this media item. If the specified aspect is contained in this instance depends on two
    /// conditions: 1) the aspect has to be present on this media item in the media storage (media library
    /// or local storage), 2) the aspect data have to be added to this instance.
    /// </summary>
    /// <param name="mediaItemAspectId">Id of the media item aspect to retrieve.</param>
    /// <returns>Media item aspect of the specified <paramref name="mediaItemAspectId"/>, or <c>null</c>,
    /// if the aspect is not contained in this instance.</returns>
    public IList<MediaItemAspect> this[Guid mediaItemAspectId]
    {
      get
      {
        IList<MediaItemAspect> result;
        return _aspects.TryGetValue(mediaItemAspectId, out result) ? result : null;
      }
    }

    /// <summary>
    /// Returns a resource locator instance for this item.
    /// </summary>
    /// <returns>Resource locator instance or <c>null</c>, if this item doesn't contain a <see cref="ProviderResourceAspect"/>.</returns>
    public IResourceLocator GetResourceLocator()
    {
      IList<MediaItemAspect> providerAspect;
      if (!_aspects.TryGetValue(ProviderResourceAspect.ASPECT_ID, out providerAspect))
        return null;
      string systemId = (string) providerAspect[0][ProviderResourceAspect.ATTR_SYSTEM_ID];
      string resourceAccessorPath = (string) providerAspect[0][ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH];
      return new ResourceLocator(systemId, ResourcePath.Deserialize(resourceAccessorPath));
    }

    public bool GetPlayData(out string mimeType, out string mediaItemTitle)
    {
      mimeType = null;
      mediaItemTitle = null;
      IList<MediaItemAspect> mediaAspect = this[MediaAspect.ASPECT_ID];
      if (mediaAspect == null)
        return false;
      mimeType = (string) mediaAspect[0][MediaAspect.ATTR_MIME_TYPE];
      mediaItemTitle = (string) mediaAspect[0][MediaAspect.ATTR_TITLE];
      return true;
    }

    XmlSchema IXmlSerializable.GetSchema()
    {
      return null;
    }

    void IXmlSerializable.ReadXml(XmlReader reader)
    {
      try
      {
        // First read attributes, then check for empty start element
        if (!reader.MoveToAttribute("Id"))
          throw new ArgumentException("Id attribute not present");
        _id = new Guid(reader.Value);
        if (reader.IsEmptyElement)
          return;
      }
      finally
      {
        reader.ReadStartElement();
      }
      while (reader.NodeType != XmlNodeType.EndElement)
      {
        MediaItemAspect mia = MediaItemAspect.Deserialize(reader);
        MediaItemAspect.AddAspect(_aspects, mia.Metadata, mia);
      }
      reader.ReadEndElement(); // MI
    }

    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
      writer.WriteAttributeString("Id", _id.ToString("D"));
      foreach (MediaItemAspect mia in _aspects.Values)
        mia.Serialize(writer);
    }

    public void Serialize(XmlWriter writer)
    {
      writer.WriteStartElement("MI"); // MediaItem
      ((IXmlSerializable) this).WriteXml(writer);
      writer.WriteEndElement(); // MediaItem
    }

    public static MediaItem Deserialize(XmlReader reader)
    {
      MediaItem result = new MediaItem();
      ((IXmlSerializable) result).ReadXml(reader);
      return result;
    }

    public override string ToString()
    {
      string mimeType;
      string title;
      if (GetPlayData(out mimeType, out title))
        return title;
      return "<Unknown>";
    }

    #region IEquatable<MediaItem> implementation

    public bool Equals(MediaItem other)
    {
      if (other == null)
        return false;
      IList<MediaItemAspect> myProviderAspect = _aspects[ProviderResourceAspect.ASPECT_ID];
      IList<MediaItemAspect> otherProviderAspect = other._aspects[ProviderResourceAspect.ASPECT_ID];
	  // TODO: FIX THIS
      return myProviderAspect[0][ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH] ==
          otherProviderAspect[0][ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH];
    }

    #endregion

    #region Base overrides

    public override int GetHashCode()
    {
      IList<MediaItemAspect> providerAspect = _aspects[ProviderResourceAspect.ASPECT_ID];
	  // TODO: FIX THIS
      return providerAspect[0][ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH].GetHashCode();
    }

    public override bool Equals(object obj)
    {
      return Equals(obj as MediaItem);
    }

    #endregion

    #region Additional members for the XML serialization

    internal MediaItem()
    {
      _aspects = new Dictionary<Guid, IList<MediaItemAspect>>();
    }

    #endregion
  }
}
