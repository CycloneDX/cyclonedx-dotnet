using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Regression tests for filtering a solution that resolves a shared dependency to two versions.
    ///
    /// Two projects of one solution reference Shared@1.0.0, which declares a dependency on Leaf with the
    /// open range [1.0.0, ). ProjectA also pins Leaf 2.0.0, so the range resolves to 2.0.0 there, while it
    /// resolves to 1.0.0 in ProjectB. Previously the merge lost one project's edges, leaving Leaf@1.0.0
    /// unreachable before filtering. The merge now preserves both edges, so excluding Shared should
    /// remove Leaf@1.0.0 while keeping the directly referenced Leaf@2.0.0.
    ///
    /// Pre-existing orphan retention is covered separately by ExcludeFilterHelperTests.
    /// </summary>
    public class Issue1133_PreExistingOrphans
    {
        private static MockFileData Source(string file)
        {
            return new MockFileData(File.ReadAllText(
                Path.Combine("FunctionalTests", "Issue1133-PreExistingOrphans", file)));
        }

        private static MockFileSystem GetMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/sln.sln"), Source("solutionsln.text") },
                { MockUnixSupport.Path("c:/ProjectPath/projectA/ProjectA.csproj"), Source("projectAcsproj.xml") },
                { MockUnixSupport.Path("c:/ProjectPath/projectA/obj/project.assets.json"), Source("projectAassets.json") },
                { MockUnixSupport.Path("c:/ProjectPath/projectB/ProjectB.csproj"), Source("projectBcsproj.xml") },
                { MockUnixSupport.Path("c:/ProjectPath/projectB/obj/project.assets.json"), Source("projectBassets.json") }
            });
        }

        private static RunOptions Options(string excludeFilter = null)
        {
            return new RunOptions
            {
                SolutionOrProjectFile = MockUnixSupport.Path("c:/ProjectPath/sln.sln"),
                outputFormat = OutputFileFormat.Json,
                DependencyExcludeFilter = excludeFilter
            };
        }

        /// <summary>
        /// Without a filter, both versions of Leaf are reported.
        /// </summary>
        [Fact]
        public async Task WithoutFilter_BothLeafVersionsArePresent()
        {
            var bom = await FunctionalTestHelper.Test(Options(), GetMockFS());

            Assert.Equal(3, bom.Components.Count);
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "2.0.0");
        }

        /// <summary>
        /// A filter that matches nothing must not change the component list.
        /// </summary>
        [Fact]
        public async Task FilterMatchingNothing_KeepsBothLeafVersions()
        {
            var bom = await FunctionalTestHelper.Test(Options("DoesNotExist"), GetMockFS());

            Assert.Equal(3, bom.Components.Count);
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "1.0.0");
        }

        /// <summary>
        /// Packages the filter names are still removed, and so are the packages it orphans.
        /// Leaf@1.0.0 is reachable only through Shared, but Leaf@2.0.0 is a direct reference of
        /// ProjectA, so Shared and Leaf@1.0.0 go while Leaf@2.0.0 stays.
        /// </summary>
        [Fact]
        public async Task ExcludedPackage_IsStillRemoved()
        {
            var bom = await FunctionalTestHelper.Test(Options("Shared"), GetMockFS());

            Assert.DoesNotContain(bom.Components, c => c.Name == "Shared");
            Assert.DoesNotContain(bom.Components, c => c.Name == "Leaf" && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "2.0.0");
        }
    }
}
