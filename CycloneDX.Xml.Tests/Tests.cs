using System;
using System.IO;
using Xunit;
using Snapshooter;
using Snapshooter.Xunit;
using CycloneDX.Xml;

namespace CycloneDX.Xml.Tests
{
    public class XmlBomDeserializerTests
    {
        [Theory] 
        [InlineData("bom")]
        [InlineData("valid-metadata-author-1.2")]
        [InlineData("valid-metadata-manufacture-1.2")]
        [InlineData("valid-metadata-supplier-1.2")]
        [InlineData("valid-metadata-timestamp-1.2")]
        public void XmlRoundTripTest(string filename)
        {
            var resourceFilename = Path.Join("Resources", filename + ".xml");
            var xmlBom = File.ReadAllText(resourceFilename);

            var bom = XmlBomDeserializer.Deserialize(xmlBom);
            xmlBom = XmlBomSerializer.Serialize(bom);

            Snapshot.Match(xmlBom, SnapshotNameExtension.Create(filename));
        }
    }
}
