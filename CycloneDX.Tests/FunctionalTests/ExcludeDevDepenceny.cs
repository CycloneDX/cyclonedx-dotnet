using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ExcludeDevDepenceny
    {
        [Fact]
        public async Task DevDependenciesNormalyGoIntoTheBom()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies.json"));
            var options = new RunOptions
            {
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 1);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0 && c.Version == "9.16.0.82469");

        }

        [Fact]
        public async Task DevDependenciesAreExcludedWithExcludeDevDependencies()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies.json"));
            var options = new RunOptions
            {
                excludeDev = true
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 0);
            Assert.True(bom.Dependencies.Count == 1); // only the meta component
        }

        [Fact]
        public async Task DevDependenciesAreExcludedIfNoRuntimepartRemainsWithExcludeDevDependencies()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "Issue847-FineGrainedDependencies", "FineGrainedDependency.csproj.project.assets.json"));
            var options = new RunOptions
            {
                excludeDev = true
            };
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal(10, bom.Dependencies.Count);
            Assert.Equal(9, bom.Components.Count);

            options.excludeDev = false;
            bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal(17, bom.Dependencies.Count);
            Assert.Equal(16, bom.Components.Count);
        }
    }
}
