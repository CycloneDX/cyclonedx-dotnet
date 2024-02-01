using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests.ExcludeDevDependenciesPackagesConfig
{
    public class ExcludeDevDependnciesWithPackageConfig
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ExcludeDevDependenciesPackagesConfig", "DevDependencies_WithPackageConfig_CsProj.xml"))) },
                { MockUnixSupport.Path("c:/ProjectPath/packages.config"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ExcludeDevDependenciesPackagesConfig", "DevDependencies_WithPackageConfig_PackageConfig.xml"))) }
            });
        }

        [Fact]
        public async Task DevDependenciesNormalyGoIntoTheBom()
        {
            var options = new RunOptions
            {
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 1, $"Unexpected number of components. Expected 1, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0 && c.Version == "9.16.0.82469");
            Assert.True(bom.Components.First(c => c.Name == "SonarAnalyzer.CSharp").Scope == Component.ComponentScope.Excluded, "Scope of development dependency is not excluded.");

        }

        [Fact]
        public async Task DevDependenciesAreExcludedWithExcludeDevDependencies()
        {
            var options = new RunOptions
            {
                excludeDev = true
            };


            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 0);


        }


    }
}
