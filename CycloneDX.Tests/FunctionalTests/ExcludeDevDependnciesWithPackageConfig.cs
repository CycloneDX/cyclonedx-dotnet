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

namespace CycloneDX.Tests.FunctionalTests
{
    public class ExcludeDevDependnciesWithPackageConfig
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_WithPackageConfig_CsProj.xml"))) },
                { MockUnixSupport.Path("c:/ProjectPath/packages.config"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_WithPackageConfig_PackageConfig.xml"))) }
            });
        }

        [Fact]
        public async Task DevDependenciesAreIncludedWithScopeExcluded()
        {
            var options = new RunOptions
            {
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 1, $"Unexpected number of components. Expected 1, got {bom.Components.Count}");
            var devDep = bom.Components.FirstOrDefault(c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0 && c.Version == "9.16.0.82469");
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);
        }

        [Fact]
        public async Task ExcludeDevFlag_IsDeprecatedAndHasNoEffect()
        {
            var options = new RunOptions
            {
                excludeDev = true
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            // flag is deprecated; dev dep must still appear with scope=excluded
            Assert.True(bom.Components.Count == 1, $"Unexpected number of components. Expected 1, got {bom.Components.Count}");
            var devDep = bom.Components.FirstOrDefault(c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0 && c.Version == "9.16.0.82469");
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);
        }
    }
}
