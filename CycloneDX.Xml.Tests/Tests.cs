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
        [InlineData("bom.xml")]
        public void XmlRoundTripTest(string filename)
        {
            var resourceFilename = Path.Join("Resources", filename);
            var xmlBom = File.ReadAllText(resourceFilename);

            var bom = XmlBomDeserializer.Deserialize(xmlBom);
            xmlBom = XmlBomSerializer.Serialize(bom);

            Snapshot.Match(xmlBom, SnapshotNameExtension.Create(filename));
        }
    }
}
