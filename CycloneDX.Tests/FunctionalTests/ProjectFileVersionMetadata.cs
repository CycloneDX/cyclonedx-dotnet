using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ProjectFileVersionMetadata
    {
        [Fact(Timeout = 15000)]
        public async Task MetadataVersionFromProjectFile()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions
            {
            };

            var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <OutputType>Exe</OutputType>\n    <PackageId>SampleProject</PackageId>\n  <Version>1.2.3</Version>\n  </PropertyGroup>\n  <ItemGroup>\n  </ItemGroup>\n</Project>\n";

            var bom = await FunctionalTestHelper.TestWithProjectFile(assetsJson, csproj, options);
            Assert.Equal("1.2.3", bom.Metadata.Component.Version);
        }
    }
}
