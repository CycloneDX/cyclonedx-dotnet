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
        [Fact]
        public void JsonRoundTripTest()
        {
            var resourceFilename = Path.Join("Resources", "bom.json");
            var jsonBom = File.ReadAllText(resourceFilename);

            var bom = JsonBomDeserializer.Deserialize(jsonBom);
            jsonBom = JsonBomSerializer.Serialize(bom);

            Snapshot.Match(jsonBom);
        }
    }
}
