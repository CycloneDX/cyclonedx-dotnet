using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    [Collection("Non-Parallel")]
    public class ExcludeDevDepenceny
    {
        // E2E counterpart: CycloneDX.E2ETests.DevDependencyTests
        [Fact]
        [Trait("Status", "MigratedToE2E")]
        public async Task DevDependenciesAreIncludedWithScopeExcluded()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies.json"));
            var options = new RunOptions
            {
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 1);
            var devDep = bom.Components.FirstOrDefault(c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0 && c.Version == "9.16.0.82469");
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);
        }

        // E2E counterpart: CycloneDX.E2ETests.DevDependencyTests
        [Fact]
        [Trait("Status", "MigratedToE2E")]
        public async Task ExcludeDevFlag_FiltersAndWarnsThatItIsDeprecated()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies.json"));
            var options = new RunOptions
            {
                excludeDev = true
            };

            using var stdout = new StringWriter();
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(stdout);
                var bom = await FunctionalTestHelper.Test(assetsJson, options);
                Assert.Empty(bom.Components);
                Assert.DoesNotContain(bom.Dependencies, d => d.Ref == "pkg:nuget/SonarAnalyzer.CSharp@9.16.0.82469");
                Assert.Contains("deprecated", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
                Assert.Contains("still filters", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
