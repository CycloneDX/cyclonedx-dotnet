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
        [Fact]
        public void XmlRoundTripTest()
        {
            var resourceFilename = Path.Join("Resources", "bom.xml");
            var xmlBom = File.ReadAllText(resourceFilename);

            var bom = XmlBomDeserializer.Deserialize(xmlBom);
            xmlBom = XmlBomSerializer.Serialize(bom);

            Snapshot.Match(xmlBom);
        }
    }
}
