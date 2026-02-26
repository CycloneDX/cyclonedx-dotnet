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
    public class ExcludeDevDepenceny
    {
        // E2E counterpart: CycloneDX.E2ETests.DevDependencyTests
        [Fact]
        [Trait("Status", "MigratedToE2E")]
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

        // E2E counterpart: CycloneDX.E2ETests.DevDependencyTests
        [Fact]
        [Trait("Status", "MigratedToE2E")]
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
    }
}
