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
    /// Regression test: --exclude-filter must not delete packages that were already unreachable.
    ///
    /// Two projects of one solution reference Shared@1.0.0, which declares a dependency on Leaf with the
    /// open range [1.0.0, ). ProjectA also pins Leaf 2.0.0, so the range resolves to 2.0.0 there, while it
    /// resolves to 1.0.0 in ProjectB. Both resolutions are correct in their own context, but the merged set
    /// keeps one entry per (name, version), so only one project's edges survive and Leaf@1.0.0 ends up
    /// referenced by nothing.
    ///
    /// --exclude-filter must not delete it: the package is shipped, and the missing edge is an artefact of
    /// merging two resolution contexts, not evidence that nothing uses it.
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
        /// Without a filter, both versions of Leaf are reported; Leaf@1.0.0 has no parent.
        /// This documents the state the filter must not act on.
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
        public async Task FilterMatchingNothing_KeepsPreExistingOrphan()
        {
            var bom = await FunctionalTestHelper.Test(Options("DoesNotExist"), GetMockFS());

            Assert.Equal(3, bom.Components.Count);
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "1.0.0");
        }

        /// <summary>
        /// Packages the filter names are still removed, and so are the packages it orphans.
        /// Shared is the only parent of Leaf@2.0.0's edge, but Leaf@2.0.0 is a direct reference of
        /// ProjectA, so only Shared itself goes.
        /// </summary>
        [Fact]
        public async Task ExcludedPackage_IsStillRemoved()
        {
            var bom = await FunctionalTestHelper.Test(Options("Shared"), GetMockFS());

            Assert.DoesNotContain(bom.Components, c => c.Name == "Shared");
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => c.Name == "Leaf" && c.Version == "2.0.0");
        }
    }
}
