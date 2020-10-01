namespace CycloneDX.Json.Tests
{
    public class Tests
    {
        [Theory]
        [InlineData("bom")]
        [InlineData("valid-metadata-author-1.2")]
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
