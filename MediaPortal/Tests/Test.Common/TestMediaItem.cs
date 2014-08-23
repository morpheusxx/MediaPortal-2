using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Test.Common
{
  internal class MediaItemAspectTypeRegistration : IMediaItemAspectTypeRegistration
  {
    protected IDictionary<Guid, MediaItemAspectMetadata> _locallyKnownMediaItemAspectTypes =
        new Dictionary<Guid, MediaItemAspectMetadata>();

    public MediaItemAspectTypeRegistration()
    {
      _locallyKnownMediaItemAspectTypes[AudioAspect.ASPECT_ID] = AudioAspect.Metadata;
      _locallyKnownMediaItemAspectTypes[ProviderResourceAspect.ASPECT_ID] = ProviderResourceAspect.Metadata;
    }

    public IDictionary<Guid, MediaItemAspectMetadata> LocallyKnownMediaItemAspectTypes
    {
      get { return _locallyKnownMediaItemAspectTypes; }
    }

    public void RegisterLocallyKnownMediaItemAspectType(MediaItemAspectMetadata miaType)
    {
      throw new NotImplementedException();
    }
  }

  [TestClass]
  public class TestMediaItem
  {
    public TestMediaItem()
    {
      ServiceRegistration.Set<IMediaItemAspectTypeRegistration>(new MediaItemAspectTypeRegistration());
    }

    [TestMethod]
    public void TestRelationships()
    {
      Guid trackId = new Guid("11111111-aaaa-aaaa-aaaa-111111111111");
      Guid albumId = new Guid("22222222-bbbb-bbbb-bbbb-222222222222");
      Guid artistId = new Guid("33333333-cccc-cccc-cccc-333333333333");

      Guid trackRelationship = new Guid("44444444-dddd-dddd-dddd-444444444444");
      Guid albumRelationship = new Guid("55555555-eeee-eeee-eeee-555555555555");
      Guid artistRelationship = new Guid("66666666-ffff-ffff-ffff-666666666666");

      MediaItem track1 = new MediaItem(trackId, new Dictionary<Guid, MediaItemAspect>(), new List<MediaItemRelationship>());
      track1.Aspects[ProviderResourceAspect.ASPECT_ID] = new MediaItemAspect(ProviderResourceAspect.Metadata);
      track1.Aspects[ProviderResourceAspect.ASPECT_ID].SetAttribute(ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH, "c:\\file.mp3");

      track1.Relationships.Add(new MediaItemRelationship(trackRelationship, albumRelationship, albumId));
      track1.Relationships.Add(new MediaItemRelationship(trackRelationship, artistRelationship, artistId));

      Assert.AreEqual(track1.Relationships[0].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track1.Relationships[0].RelationshipType, albumRelationship, "Track -> album relationship type");
      Assert.AreEqual(track1.Relationships[0].RelationshipId, albumId, "Track -> album relationship ID");
      Assert.AreEqual(track1.Relationships[1].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track1.Relationships[1].RelationshipType, artistRelationship, "Track -> album relationship type");
      Assert.AreEqual(track1.Relationships[1].RelationshipId, artistId, "Track -> album relationship ID");

      TextWriter writer = new StringWriter();
      XmlWriter serialiser = new XmlTextWriter(writer);
      serialiser.WriteStartElement("Test"); // Wrapper around the tracks
      track1.Serialize(serialiser);
      track1.Serialize(serialiser);
      serialiser.WriteEndElement();

      //Console.WriteLine("XML: {0}", writer.ToString());
      //Assert.AreEqual("<MI Id=\"" + trackId + "\"><Relationship ItemType=\"" + AudioAspect.RELATIONSHIP_TRACK + "\" RelationshipType=\"" + AlbumAspect.RELATIONSHIP_ALBUM + "\" RelationshipId=\"" + albumId + "\" /></MI>", trackText.ToString(), "Track XML");

      XmlReader reader = XmlReader.Create(new StringReader(writer.ToString()));
      reader.Read(); // Test
      //Console.WriteLine("Reader state Test, {0} {1}", reader.NodeType, reader.Name);

      reader.Read(); // MI
      //Console.WriteLine("Reader state track2, {0} {1}", reader.NodeType, reader.Name);
      MediaItem track2 = MediaItem.Deserialize(reader);
      Assert.AreEqual(track2.Aspects[ProviderResourceAspect.ASPECT_ID].GetAttributeValue(ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH), "c:\\file.mp3", "Track location");
      Assert.IsTrue(track2.Relationships.Count == 2, "Track relationship count");
      Assert.AreEqual(track2.Relationships[0].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track2.Relationships[0].RelationshipType, albumRelationship, "Track -> album relationship type");
      Assert.AreEqual(track2.Relationships[0].RelationshipId, albumId, "Track -> album relationship ID");
      Assert.AreEqual(track2.Relationships[1].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track2.Relationships[1].RelationshipType, artistRelationship, "Track -> album relationship type");
      Assert.AreEqual(track2.Relationships[1].RelationshipId, artistId, "Track -> album relationship ID");

      //Console.WriteLine("Reader state track3, {0} {1}", reader.NodeType, reader.Name);
      MediaItem track3 = MediaItem.Deserialize(reader);
      Assert.AreEqual(track3.Aspects[ProviderResourceAspect.ASPECT_ID].GetAttributeValue(ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH), "c:\\file.mp3", "Track location");
      Assert.IsTrue(track3.Relationships.Count == 2, "Track relationship count");
      Assert.AreEqual(track3.Relationships[0].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track3.Relationships[0].RelationshipType, albumRelationship, "Track -> album relationship type");
      Assert.AreEqual(track3.Relationships[0].RelationshipId, albumId, "Track -> album relationship ID");
      Assert.AreEqual(track3.Relationships[1].ItemType, trackRelationship, "Track -> album item type");
      Assert.AreEqual(track3.Relationships[1].RelationshipType, artistRelationship, "Track -> album relationship type");
      Assert.AreEqual(track3.Relationships[1].RelationshipId, artistId, "Track -> album relationship ID");

      reader.Read(); // Test
    }
  }
}
