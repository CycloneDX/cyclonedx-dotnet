using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class SpecVersion
    {
        [Fact]
        public async Task TestSpecifiedSpecVersionIsUsed()
        {
            var assetsJson = await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions { specVersion = SpecificationVersion.v1_5 };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal(SpecificationVersion.v1_5, bom.SpecVersion);
        }

        [Fact]
        public async Task CurrentSpecVersionUsedWhenNoneSpecified()
        {
            var assetsJson = await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions();

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal(SpecificationVersionHelpers.CurrentVersion, bom.SpecVersion);
        }
    }
}
