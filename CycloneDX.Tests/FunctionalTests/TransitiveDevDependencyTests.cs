using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class TransitiveDevDependencyTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PostMerge_UnresolvedRuntimeRangePromotesDevelopmentOccurrenceAndDescendants(bool excludeDev)
        {
            // The production target has an unresolved edge, while only a development
            // target supplies Shared@1 and its descendants. Local excluded scope must
            // not prevent runtime reachability from being completed after merging.
            var consumer = new DotnetDependency
            {
                Name = "Consumer", Version = "1.0.0", IsDirectReference = true,
                IsRuntimeReachable = true, IsNonDevelopmentReachable = true,
                Dependencies = new Dictionary<string, string> { ["Shared"] = "[1.0.0,2.0.0)" }
            };
            var unresolvedEdge = new KeyValuePair<string, string>("Shared", "[1.0.0,2.0.0)");
            consumer.RuntimeUnresolvedDependencyEdges.Add(unresolvedEdge);
            consumer.NonDevelopmentUnresolvedDependencyEdges.Add(unresolvedEdge);
            var packages = new HashSet<DotnetDependency>
            {
                consumer,
                new DotnetDependency
                {
                    Name = "BuildTool", Version = "1.0.0", IsDirectReference = true,
                    IsDevDependency = true, IsRuntimeReachable = false,
                    IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded,
                    Dependencies = new Dictionary<string, string> { ["Shared"] = "1.0.0" }
                },
                new DotnetDependency
                {
                    Name = "Shared", Version = "1.0.0", IsRuntimeReachable = false,
                    IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded,
                    Dependencies = new Dictionary<string, string> { ["Leaf"] = "1.0.0" }
                },
                new DotnetDependency
                {
                    Name = "Leaf", Version = "1.0.0", IsRuntimeReachable = false,
                    IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded
                },
                // Another version exists, but does not satisfy the consumer's range.
                new DotnetDependency
                {
                    Name = "Shared", Version = "2.0.0", IsDirectReference = true,
                    IsRuntimeReachable = true, IsNonDevelopmentReachable = true
                }
            };
            var bom = await RunMergedPackages(packages, excludeDev);
            var shared = Assert.Single(bom.Components, c => c.Name == "Shared" && c.Version == "1.0.0");
            var leaf = Assert.Single(bom.Components, c => c.Name == "Leaf");
            Assert.Equal(Component.ComponentScope.Required, shared.Scope);
            Assert.Equal(Component.ComponentScope.Required, leaf.Scope);
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "pkg:nuget/Consumer@1.0.0", shared.BomRef);
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, shared.BomRef, leaf.BomRef);
            Assert.DoesNotContain(bom.Dependencies, d => d.Ref == "pkg:nuget/Consumer@1.0.0" &&
                d.Dependencies.Any(child => child.Ref == "pkg:nuget/Shared@2.0.0"));
            if (excludeDev)
                Assert.DoesNotContain(bom.Components, c => c.Name == "BuildTool");
            else
                Assert.Equal(Component.ComponentScope.Excluded,
                    Assert.Single(bom.Components, c => c.Name == "BuildTool").Scope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PostMerge_PromotionThroughPreviouslyVisitedRuntimeBridgePromotesLeaf(bool excludeDev)
        {
            var bridge = new DotnetDependency
            {
                Name = "Bridge", Version = "1.0.0", IsDirectReference = true,
                IsRuntimeReachable = true, IsNonDevelopmentReachable = true,
                Scope = Component.ComponentScope.Required
            };
            // This edge came from a merged development occurrence of Bridge, not
            // its original runtime target. It is followed once Bridge is promoted.
            bridge.AdditionalDependencyEdges.Add(new KeyValuePair<string, string>("Leaf", "1.0.0"));
            var consumer = new DotnetDependency
            {
                Name = "Consumer", Version = "1.0.0", IsDirectReference = true,
                IsRuntimeReachable = true, IsNonDevelopmentReachable = true,
                Dependencies = new Dictionary<string, string> { ["Shared"] = "[1.0.0,2.0.0)" }
            };
            var unresolvedEdge = new KeyValuePair<string, string>("Shared", "[1.0.0,2.0.0)");
            consumer.RuntimeUnresolvedDependencyEdges.Add(unresolvedEdge);
            consumer.NonDevelopmentUnresolvedDependencyEdges.Add(unresolvedEdge);
            var packages = new HashSet<DotnetDependency>
            {
                // Seed Bridge before Consumer, so Bridge is visited before Shared
                // promotes it. Promotion must revisit its outgoing merged edges.
                bridge,
                consumer,
                new DotnetDependency
                {
                    Name = "Shared", Version = "1.0.0", IsRuntimeReachable = false,
                    IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded,
                    Dependencies = new Dictionary<string, string> { ["Bridge"] = "1.0.0" }
                },
                new DotnetDependency
                {
                    Name = "Leaf", Version = "1.0.0", IsRuntimeReachable = false,
                    IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded
                }
            };

            var bom = await RunMergedPackages(packages, excludeDev);

            var leaf = Assert.Single(bom.Components, c => c.Name == "Leaf");
            Assert.Equal(Component.ComponentScope.Required, leaf.Scope);
            Assert.Equal(4, bom.Components.Count);
            Assert.All(bom.Components, c => Assert.Equal(Component.ComponentScope.Required, c.Scope));
            FunctionalTestHelper.AssertHasDependencyWithChild(bom,
                "pkg:nuget/Consumer@1.0.0", "pkg:nuget/Shared@1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom,
                "pkg:nuget/Shared@1.0.0", "pkg:nuget/Bridge@1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom,
                "pkg:nuget/Bridge@1.0.0", leaf.BomRef);
        }

        private static async Task<Bom> RunMergedPackages(HashSet<DotnetDependency> packages, bool excludeDev)
        {
            var solutionPath = MockUnixSupport.Path("c:/ProjectPath/Project.sln");
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                [solutionPath] = new MockFileData("")
            });
            var solutionService = new Mock<ISolutionFileService>();
            solutionService.Setup(s => s.GetSolutionDotnetDependencys(
                solutionPath, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(packages);
            var nugetService = new Mock<INugetService>();
            nugetService.Setup(s => s.GetComponentAsync(It.IsAny<DotnetDependency>()))
                .Returns((DotnetDependency dependency) => Task.FromResult(new Component
                {
                    Name = dependency.Name, Version = dependency.Version,
                    Type = Component.Classification.Library, Scope = dependency.Scope,
                    BomRef = $"pkg:nuget/{dependency.Name}@{dependency.Version}"
                }));
            var nugetFactory = new Mock<INugetServiceFactory>();
            nugetFactory.Setup(f => f.Create(It.IsAny<RunOptions>(), It.IsAny<IFileSystem>(),
                It.IsAny<IGithubService>(), It.IsAny<List<string>>())).Returns(nugetService.Object);
            var runner = new Runner(fileSystem, null, null, null, null, null,
                solutionService.Object, nugetFactory.Object);

            var exitCode = await runner.HandleCommandAsync(new RunOptions
            {
                SolutionOrProjectFile = solutionPath, disablePackageRestore = true,
                outputDirectory = "/bom/", excludeDev = excludeDev
            });

            Assert.Equal((int)ExitCode.OK, exitCode);
            return runner.LastGeneratedBom;
        }

        [Fact]
        public async System.Threading.Tasks.Task ExcludeDev_RemovesDevelopmentOnlyRootsAndTransitives()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_TransitiveOnlyViaDev.json"));
            var bom = await FunctionalTestHelper.Test(assetsJson, new RunOptions { excludeDev = true });

            Assert.DoesNotContain(bom.Components, c => c.Name == "SonarAnalyzer.CSharp" || c.Name == "Google.Protobuf");
            Assert.All(bom.Dependencies, dependency =>
            {
                Assert.DoesNotContain("SonarAnalyzer.CSharp", dependency.Ref);
                Assert.DoesNotContain("Google.Protobuf", dependency.Ref);
                Assert.All(dependency.Dependencies ?? [], child =>
                {
                    Assert.DoesNotContain("SonarAnalyzer.CSharp", child.Ref);
                    Assert.DoesNotContain("Google.Protobuf", child.Ref);
                });
            });
        }

        [Fact]
        public async System.Threading.Tasks.Task ExcludeDev_RetainsRuntimeSharedTransitiveAndItsGraphEdge()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_SharedTransitive.json"));
            var bom = await FunctionalTestHelper.Test(assetsJson, new RunOptions { excludeDev = true });

            Assert.DoesNotContain(bom.Components, c => c.Name == "SonarAnalyzer.CSharp");
            var runtime = Assert.Single(bom.Components, c => c.Name == "Newtonsoft.Json");
            var shared = Assert.Single(bom.Components, c => c.Name == "Google.Protobuf");
            Assert.Equal(Component.ComponentScope.Required, runtime.Scope);
            Assert.Equal(Component.ComponentScope.Required, shared.Scope);
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, runtime.BomRef, shared.BomRef);
        }

        /// <summary>
        /// A package that is only reachable as a transitive dependency of a dev
        /// dependency (and not from any runtime path) must receive scope=excluded.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task TransitiveDependency_ReachableOnlyViaDevDep_IsExcluded()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_TransitiveOnlyViaDev.json"));
            var options = new RunOptions();

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // SonarAnalyzer.CSharp is the direct dev dep — must be excluded.
            var devDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0);
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);

            // Google.Protobuf is only reachable through the dev dep — must also be excluded.
            var transitiveDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.NotNull(transitiveDep);
            Assert.Equal(Component.ComponentScope.Excluded, transitiveDep.Scope);
        }

        /// <summary>
        /// A package that is reachable from both a dev dependency and a runtime
        /// dependency must keep scope=required (the runtime path wins).
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task TransitiveDependency_ReachableViaDevDepAndRuntimeDep_IsRequired()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_SharedTransitive.json"));
            var options = new RunOptions();

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // SonarAnalyzer.CSharp is the dev dep — must be excluded.
            var devDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0);
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);

            // Newtonsoft.Json is a direct runtime dep — must be required.
            var runtimeDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Newtonsoft.Json", true) == 0);
            Assert.NotNull(runtimeDep);
            Assert.Equal(Component.ComponentScope.Required, runtimeDep.Scope);

            // Google.Protobuf is reachable via both the dev dep AND Newtonsoft.Json —
            // the runtime path wins, so it must be required.
            var sharedTransitive = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.NotNull(sharedTransitive);
            Assert.Equal(Component.ComponentScope.Required, sharedTransitive.Scope);
        }
    }
}
