using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;


namespace CycloneDX.Tests.FunctionalTests
{
    public class RunOptionsValidation
    {
        [Fact(Timeout = 15000)]
        public async Task Defaults()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions
            {
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal("Project", bom.Metadata.Component.Name);
            Assert.Equal("0.0.0", bom.Metadata.Component.Version);
            Assert.Equal("Project@0.0.0", bom.Metadata.Component.BomRef);
            Assert.Null(bom.Metadata.Component.Purl);
        }

        [Fact(Timeout = 15000)]
        public async Task SetNugetPurl()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions
            {
                setName = "MyComponent",
                setVersion = "1.2.3",
                setNugetPurl = true
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal("MyComponent", bom.Metadata.Component.Name);
            Assert.Equal("1.2.3", bom.Metadata.Component.Version);
            Assert.Equal("pkg:nuget/MyComponent@1.2.3", bom.Metadata.Component.BomRef);
            Assert.Equal("pkg:nuget/MyComponent@1.2.3", bom.Metadata.Component.Purl);
        }
    }
}
