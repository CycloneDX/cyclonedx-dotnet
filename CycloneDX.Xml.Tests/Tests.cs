using System.IO;

namespace CycloneDX.Xml.Tests
{
    public class XmlBomDeserializerTests
    {
        [Theory] 
        [InlineData("bom")] 
        [InlineData("valid-metadata-author-1.2")]
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
