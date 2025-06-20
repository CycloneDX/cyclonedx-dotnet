using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ProjectVersionMetadataTest
    {
        [Fact]
        public async Task BomMetadataVersion_ShouldMatchProjectVersion()
        {
            var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                        "  <PropertyGroup>\n" +
                        "    <OutputType>Exe</OutputType>\n" +
                        "    <Version>2.1.3</Version>\n" +
                        "  </PropertyGroup>\n" +
                        "</Project>\n";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path("c:/ProjectPath/Project.csproj"), new MockFileData(csproj) },
                { XFS.Path("c:/ProjectPath/obj/project.assets.json"), new MockFileData("{}") }
            });

            var options = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path("c:/ProjectPath/Project.csproj"),
                outputDirectory = XFS.Path("c:/ProjectPath/"),
                disablePackageRestore = true
            };

            var bom = await FunctionalTestHelper.Test(options, mockFileSystem);
            Assert.Equal("2.1.3", bom.Metadata.Component.Version);
        }
    }
}
