using System;
using System.IO;
using Xunit;
using Snapshooter;
using Snapshooter.Xunit;
using CycloneDX.Json;

namespace CycloneDX.Json.Tests
{
    public class Tests
    {
        [Theory]
        [InlineData("bom")]
        [InlineData("valid-metadata-author-1.2")]
        [InlineData("valid-metadata-manufacture-1.2")]
        [InlineData("valid-metadata-supplier-1.2")]
        [InlineData("valid-metadata-timestamp-1.2")]
        [InlineData("valid-metadata-tool-1.2")]
        [InlineData("valid-component-hashes-1.2")]
        public void JsonRoundTripTest(string filename)
        {
            var resourceFilename = Path.Join("Resources", filename + ".json");
            var jsonBom = File.ReadAllText(resourceFilename);

            var bom = JsonBomDeserializer.Deserialize(jsonBom);
            jsonBom = JsonBomSerializer.Serialize(bom);

            Snapshot.Match(jsonBom, SnapshotNameExtension.Create(filename));
        }
    }
}
