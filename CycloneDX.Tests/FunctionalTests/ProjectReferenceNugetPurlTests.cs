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
    public class ProjectReferenceNugetPurlTests
    {
        private const string DefaultRef = "ClassLibrary1@1.0.0";
        private const string GeneratedPurl = "pkg:nuget/ClassLibrary1@1.0.0";
        // Deliberately different from the guessed NuGet identity.
        private const string ExistingPurl = "pkg:nuget/Published.Library@2.3.4";
        private const string CustomRef = "urn:project:class-library";

        [Theory]
        [InlineData(ExistingPurl, CustomRef, ExistingPurl, CustomRef)]
        [InlineData(ExistingPurl, DefaultRef, ExistingPurl, ExistingPurl)]
        [InlineData(ExistingPurl, "", ExistingPurl, ExistingPurl)]
        [InlineData(ExistingPurl, null, ExistingPurl, ExistingPurl)]
        [InlineData(null, DefaultRef, GeneratedPurl, GeneratedPurl)]
        [InlineData("", DefaultRef, GeneratedPurl, GeneratedPurl)]
        [InlineData(null, "", GeneratedPurl, GeneratedPurl)]
        [InlineData(null, null, GeneratedPurl, GeneratedPurl)]
        [InlineData(null, CustomRef, GeneratedPurl, CustomRef)]
        [InlineData("", CustomRef, GeneratedPurl, CustomRef)]
        public async Task BothFlags_PreserveOrGenerateIdentifiersAndWireGraph(
            string purl, string bomRef, string expectedPurl, string expectedRef)
        {
            var bom = await RunWithProjectComponent(purl, bomRef, true, true);

            var project = Assert.Single(bom.Components, c => c.Name == "ClassLibrary1");
            Assert.Equal(expectedPurl, project.Purl);
            Assert.Equal(expectedRef, project.BomRef);
            AssertProjectGraph(bom, expectedRef);
        }

        [Theory]
        [InlineData(null, DefaultRef)]
        [InlineData(ExistingPurl, DefaultRef)]
        [InlineData(ExistingPurl, CustomRef)]
        public async Task IncludeOnly_DoesNotRewriteIdentifiers(string purl, string bomRef)
        {
            var bom = await RunWithProjectComponent(purl, bomRef, true, false);

            var project = Assert.Single(bom.Components, c => c.Name == "ClassLibrary1");
            Assert.Equal(purl, project.Purl);
            Assert.Equal(bomRef, project.BomRef);
            Assert.Equal("Project@0.0.0", bom.Metadata.Component.BomRef);
            AssertProjectGraph(bom, bomRef);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WithoutInclude_ProjectNodesAreExcludedAndPackagesFlattened(bool setNugetPurl)
        {
            var bom = await RunWithProjectComponent(ExistingPurl, CustomRef, false, setNugetPurl);

            Assert.Equal(setNugetPurl ? "pkg:nuget/Project@0.0.0" : "Project@0.0.0",
                bom.Metadata.Component.BomRef);
            Assert.Equal(3, bom.Components.Count);
            Assert.DoesNotContain(bom.Components, c => c.Name == "ClassLibrary1");
            var root = Assert.Single(bom.Dependencies, d => d.Ref == bom.Metadata.Component.BomRef);
            Assert.Equal(new[] { "pkg:nuget/Moq@4.20.70" }, root.Dependencies.Select(d => d.Ref));
            AssertPackageGraph(bom);
            AssertGraphReferencesResolve(bom);
        }

        private static void AssertProjectGraph(Bom bom, string projectRef)
        {
            Assert.Equal(4, bom.Components.Count);
            var root = Assert.Single(bom.Dependencies, d => d.Ref == bom.Metadata.Component.BomRef);
            Assert.Equal(new[] { projectRef }, root.Dependencies.Select(d => d.Ref));
            var project = Assert.Single(bom.Dependencies, d => d.Ref == projectRef);
            Assert.Equal(new[] { "pkg:nuget/Moq@4.20.70" }, project.Dependencies.Select(d => d.Ref));
            AssertPackageGraph(bom);
            AssertGraphReferencesResolve(bom);
        }

        private static void AssertPackageGraph(Bom bom)
        {
            var moq = Assert.Single(bom.Dependencies, d => d.Ref == "pkg:nuget/Moq@4.20.70");
            Assert.Equal(new[] { "pkg:nuget/Castle.Core@5.1.1" }, moq.Dependencies.Select(d => d.Ref));
            var castle = Assert.Single(bom.Dependencies, d => d.Ref == "pkg:nuget/Castle.Core@5.1.1");
            Assert.Equal(new[] { "pkg:nuget/System.Diagnostics.EventLog@6.0.0" }, castle.Dependencies.Select(d => d.Ref));
            var eventLog = Assert.Single(bom.Dependencies, d => d.Ref == "pkg:nuget/System.Diagnostics.EventLog@6.0.0");
            Assert.Empty(eventLog.Dependencies);
        }

        private static void AssertGraphReferencesResolve(Bom bom)
        {
            var refs = bom.Components.Select(c => c.BomRef).Append(bom.Metadata.Component.BomRef).ToHashSet();
            Assert.Equal(refs.Count, bom.Dependencies.Count);
            Assert.All(bom.Dependencies, d =>
            {
                Assert.Contains(d.Ref, refs);
                Assert.All(d.Dependencies, child => Assert.Contains(child.Ref, refs));
            });
        }

        private static async Task<Bom> RunWithProjectComponent(
            string purl, string bomRef, bool includeProjectReferences, bool setNugetPurl)
        {
            var projectPath = MockUnixSupport.Path("c:/ProjectPath/Project.csproj");
            var assetsPath = MockUnixSupport.Path("c:/ProjectPath/obj/project.assets.json");
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                [projectPath] = new MockFileData(FunctionalTestHelper.CsprojContents),
                [assetsPath] = new MockFileData(File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles",
                    "ProjectReferenceWithPackageReferenceWithTransitivePackage.json")))
            });
            var assetsService = new ProjectAssetsFileService(fileSystem, () => new AssetFileReader());
            var projectService = new Mock<IProjectFileService>();
            projectService.Setup(s => s.GetProjectDotnetDependencysAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(() => assetsService.GetDotnetDependencys(projectPath, assetsPath, false));
            projectService.Setup(s => s.GetComponent(It.IsAny<DotnetDependency>()))
                .Returns((DotnetDependency dep) => new Component
                {
                    Name = dep.Name, Version = dep.Version, Type = Component.Classification.Library,
                    Purl = purl, BomRef = bomRef, Scope = dep.Scope
                });
            var nugetService = new Mock<INugetService>();
            nugetService.Setup(s => s.GetComponentAsync(It.IsAny<DotnetDependency>()))
                .ReturnsAsync((DotnetDependency dep) => new Component
                {
                    Name = dep.Name, Version = dep.Version, Type = Component.Classification.Library,
                    Purl = $"pkg:nuget/{dep.Name}@{dep.Version}",
                    BomRef = $"pkg:nuget/{dep.Name}@{dep.Version}", Scope = dep.Scope
                });
            var factory = new Mock<INugetServiceFactory>();
            factory.Setup(s => s.Create(It.IsAny<RunOptions>(), It.IsAny<IFileSystem>(),
                    It.IsAny<IGithubService>(), It.IsAny<List<string>>()))
                .Returns(nugetService.Object);
            var runner = new Runner(fileSystem, null, null, null, null, projectService.Object, null, factory.Object);
            var exitCode = await runner.HandleCommandAsync(new RunOptions
            {
                SolutionOrProjectFile = projectPath, disablePackageRestore = true,
                outputDirectory = "/bom/", outputFormat = OutputFileFormat.Json,
                includeProjectReferences = includeProjectReferences, setNugetPurl = setNugetPurl
            });
            Assert.Equal((int)ExitCode.OK, exitCode);
            return runner.LastGeneratedBom;
        }
    }
}
