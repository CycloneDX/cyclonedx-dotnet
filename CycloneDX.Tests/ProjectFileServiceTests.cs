// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using CycloneDX.Interfaces;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Moq;
using CycloneDX.Models;
using CycloneDX.Services;
using System.IO.Abstractions;
using System.Linq;
using System.IO;
using System.Xml.Schema;

namespace CycloneDX.Tests
{
    public class ProjectFileServiceTests
    {
        private ProjectFileService GetInstanceOfProjectFileService()
        {
            var fileSystem = new FileSystem();
            var dotnetCommandService = new DotnetCommandService();
            return new ProjectFileService(
                fileSystem,
                new DotnetUtilsService(fileSystem, dotnetCommandService),
                new PackagesFileService(fileSystem),
                new ProjectAssetsFileService(fileSystem, () => new AssetFileReader()));
        }

        [Theory]
        [InlineData("", @"C:\Projects\Foo\obj\project.assets.json", false)] // expected file exists
        [InlineData("", @"C:\Projects\artifacts\obj\Foo\project.assets.json", true)] // expected missing, service finds it
        [InlineData(@"C:\build", @"C:\build\obj\Foo\project.assets.json", false)] // using baseIntermediateOutputPath        
        public void GetProjectAssetsFilePath_CoversAllScenarios(
            string baseOutputPath, 
            string assetsPath,
            bool dotnetServiceShouldBeUsed)
        {

            baseOutputPath = XFS.Path(baseOutputPath);
            assetsPath = XFS.Path(assetsPath);

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(assetsPath), "" }
                });

            string projectPath = XFS.Path(@"C:\Projects\Foo\Foo.csproj");
            // Arrange
            var dotnetMock = new Mock<IDotnetUtilsService>();
            var service = new ProjectFileService(mockFileSystem, dotnetMock.Object, null, null);

            dotnetMock.Setup(d => d.GetAssetsPath(projectPath))
                .Returns(new DotnetUtilsResult<string>
                {
                    ErrorMessage = null,
                    Result = assetsPath
                });            

            // Act
            var result = service.GetProjectAssetsFilePath(projectPath, baseOutputPath);

            // Assert
            Assert.Equal(assetsPath, result);
            dotnetMock.Verify(d => d.GetAssetsPath(It.IsAny<string>()), dotnetServiceShouldBeUsed ? Times.Once : Times.Never);            
        }

        [Fact]        
        public void IsTestProjectTrue()
        {
            string szProjectPath = System.Environment.CurrentDirectory + XFS.Path(@"\..\..\..\..\CycloneDX.Tests\CycloneDX.Tests.csproj");
            Assert.True(GetInstanceOfProjectFileService().IsTestProject(szProjectPath));
        }

        [Fact]        
        public void IsTestProjectFalse()
        {
            string szProjectPath = System.Environment.CurrentDirectory + XFS.Path(@"\..\..\..\..\CycloneDX\CycloneDX.csproj");
            Assert.False(GetInstanceOfProjectFileService().IsTestProject(szProjectPath));
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithProjectAssetsFile_ReturnsDotnetDependency()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                 .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                 .Returns(new DotnetUtilsResult<string>() { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>
                {
                    new DotnetDependency { Name = "Package", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectDotnetDependencysAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithProjectAssetsFileWithoutRestore_ReturnsDotnetDependency()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ApplicationException("Restore should not be called"));
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string>() { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>
                {
                    new DotnetDependency { Name = "Package", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);
            projectFileService.DisablePackageRestore = true;

            var packages = await projectFileService.GetProjectDotnetDependencysAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithProjectAssetsFile_ReturnsMultipleDotnetDependencys()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string>() { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>
                {
                    new DotnetDependency { Name = "Package1", Version = "1.2.3" },
                    new DotnetDependency { Name = "Package2", Version = "1.2.3" },
                    new DotnetDependency { Name = "Package3", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectDotnetDependencysAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);
            var sortedPackages = new List<DotnetDependency>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name));
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithPackagesConfig_ReturnsDotnetDependency()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                    { XFS.Path(@"c:\Project\packages.config"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string>() { Result = ""});
            var mockPackageFileService = new Mock<IPackagesFileService>();
            mockPackageFileService
                .Setup(s => s.GetDotnetDependencysAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    new HashSet<DotnetDependency>
                    {
                        new DotnetDependency { Name = "Package", Version = "1.2.3" },
                    }
                );
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectDotnetDependencysAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithPackagesConfig_ReturnsMultipleDotnetDependencys()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                    { XFS.Path(@"c:\Project\packages.config"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
             .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
             .Returns(new DotnetUtilsResult<string>() { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            mockPackageFileService
                .Setup(s => s.GetDotnetDependencysAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    new HashSet<DotnetDependency>
                    {
                    new DotnetDependency { Name = "Package1", Version = "1.2.3" },
                    new DotnetDependency { Name = "Package2", Version = "1.2.3" },
                    new DotnetDependency { Name = "Package3", Version = "1.2.3" },
                    }
                );
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectDotnetDependencysAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);
            var sortedPackages = new List<DotnetDependency>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name));
        }

        [Fact]
        public async Task GetProjectReferences_ReturnsProjectReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new[] {
                            @"..\Project1\Project1.csproj",
                            @"..\Project2\Project2.csproj",
                            @"..\Project3\Project3.csproj",
                        })
                    },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.GetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project\Project.csproj")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();

            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        [Fact]
        public async Task RecursivelyGetProjectReferences_ReturnsProjectReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new[] {
                            @"..\Project2\Project2.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new[] {
                            @"..\Project3\Project3.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), new MockFileData(@"<Project></Project>") },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects.Select(d => d.Path));
            sortedProjects.Sort();

            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        /// <summary>
        /// Reproduces issue #917: <AssemblyName>$(RootNamespace)</AssemblyName> is a common idiom
        /// copy-pasted across projects. GetAssemblyNameAndVersion previously returned the raw,
        /// unresolved literal "$(RootNamespace)" for every such project, so when a root project
        /// references two or more of them, RecursivelyGetProjectReferencesAsync's
        /// ToDictionary(project => project.name, ...) throws
        /// "An item with the same key has already been added. Key: $(RootNamespace)" even though
        /// the projects' actual (MSBuild-resolved) namespaces are different.
        /// </summary>
        [Theory]
        [InlineData("Company.Project2", "Company.Project3")]
        [InlineData("Company.$(MSBuildProjectName)", "Company.$(MSBuildProjectName)")]
        public async Task RecursivelyGetProjectReferences_TwoReferencedProjectsUseRootNamespaceAssemblyNameIdiom_DoesNotThrow(string project2Namespace, string project3Namespace)
        {
            string project2Xml = $@"<Project>
  <PropertyGroup>
    <RootNamespace>{project2Namespace}</RootNamespace>
    <AssemblyName>$(RootNamespace)</AssemblyName>
  </PropertyGroup>
</Project>";
            string project3Xml = $@"<Project>
  <PropertyGroup>
    <RootNamespace>{project3Namespace}</RootNamespace>
    <AssemblyName>$(RootNamespace)</AssemblyName>
  </PropertyGroup>
</Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new[] {
                            @"..\Project2\Project2.csproj",
                            @"..\Project3\Project3.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), new MockFileData(project2Xml) },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), new MockFileData(project3Xml) },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj")).ConfigureAwait(true);

            var names = projects.Select(p => p.Name).OrderBy(n => n).ToList();
            Assert.Equal(new[] { "Company.Project2", "Company.Project3", "Project1" }, names);
        }

        [Fact]
        public async Task RecursivelyGetProjectReferences_ReturnsCSAssemblyVersion()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), new MockFileData(@"<Project></Project>") },
                { XFS.Path(@"c:\SolutionPath\Project1\Properties\AssemblyInfo.cs"), new MockFileData(@"[assembly: AssemblyVersion(""3.2.1.0"")]")}
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj")).ConfigureAwait(true);

            Assert.Equal("3.2.1.0", projects.FirstOrDefault().Version);
        }

        [Fact]
        public async Task RecursivelyGetProjectReferences_ReturnsVBAssemblyVersion()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\SolutionPath\Project1\Project1.vbproj"), new MockFileData(@"<Project></Project>") },
                { XFS.Path(@"c:\SolutionPath\Project1\My Project\AssemblyInfo.vb"), new MockFileData(@"<Assembly: AssemblyVersion(""3.2.1.0"")>")}
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.vbproj")).ConfigureAwait(true);

            Assert.Equal("3.2.1.0", projects.FirstOrDefault().Version);
        }

        [Fact]
        public async Task RecursivelyGetProjectReferences_ReturnsXSharpAssemblyVersion()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\SolutionPath\Project1\Project1.xsproj"), new MockFileData(@"<Project></Project>") },
                { XFS.Path(@"c:\SolutionPath\Project1\Properties\AssemblyInfo.prg"), new MockFileData(@"[assembly: AssemblyVersion(""3.2.1.0"")]")}
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.xsproj")).ConfigureAwait(true);

            Assert.Equal("3.2.1.0", projects.FirstOrDefault().Version);
        }

        [Fact]
        public async Task RecursivelyGetProjectDotnetDependencys_WithAssetsFile_WritesRecursiveWarning()
        {
            // Arrange – root project has an obj/project.assets.json, which means NuGet already
            // recorded the full package closure. The warning should be written to stderr.
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Project\Project.csproj"), new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk"" />") },
                { XFS.Path(@"c:\Project\obj\project.assets.json"), new MockFileData("{}") },
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string> { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var originalError = Console.Error;
            using var capturedError = new StringWriter();
            Console.SetError(capturedError);
            try
            {
                await projectFileService.RecursivelyGetProjectDotnetDependencysAsync(
                    XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);
            }
            finally
            {
                Console.SetError(originalError);
            }

            Assert.Contains("Consider removing --recursive", capturedError.ToString());
        }

        [Fact]
        public async Task RecursiveMerge_PreservesRuntimeUseAndMultipleChildVersions()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                ["/solution/root/root.csproj"] = Helpers.GetProjectFileWithProjectReferences(new[] { "../child/child.csproj" }),
                ["/solution/child/child.csproj"] = Helpers.GetEmptyProjectFile(),
                ["/solution/root/obj/project.assets.json"] = "{}",
                ["/solution/child/obj/project.assets.json"] = "{}"
            });
            var utils = new Mock<IDotnetUtilsService>();
            utils.Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new DotnetUtilsResult());
            utils.Setup(s => s.GetAssetsPath(It.IsAny<string>())).Returns(new DotnetUtilsResult<string> { Result = "" });
            var assets = new Mock<IProjectAssetsFileService>();
            var rootProjectPath = fileSystem.Path.GetFullPath("/solution/root/root.csproj");
            var childProjectPath = fileSystem.Path.GetFullPath("/solution/child/child.csproj");
            assets.Setup(s => s.GetDotnetDependencys(rootProjectPath, It.IsAny<string>(), false)).Returns(new HashSet<DotnetDependency>
            {
                new() { Name = "Shared", Version = "1.0.0", IsDevDependency = true, IsRuntimeReachable = false,
                    Scope = Component.ComponentScope.Excluded, Dependencies = new Dictionary<string, string> { ["Child"] = "1.0.0" } }
            });
            assets.Setup(s => s.GetDotnetDependencys(childProjectPath, It.IsAny<string>(), false)).Returns(new HashSet<DotnetDependency>
            {
                new() { Name = "Shared", Version = "1.0.0", IsRuntimeReachable = true, Scope = Component.ComponentScope.Required,
                    Dependencies = new Dictionary<string, string> { ["Child"] = "2.0.0" } },
                new() { Name = "Child", Version = "1.0.0" },
                new() { Name = "Child", Version = "2.0.0" }
            });
            var service = new ProjectFileService(fileSystem, utils.Object, new Mock<IPackagesFileService>().Object, assets.Object);
            var packages = await service.RecursivelyGetProjectDotnetDependencysAsync(rootProjectPath, "", false, "", "").ConfigureAwait(true);
            var shared = packages.Single(p => p.Name == "Shared");
            Assert.True(shared.IsRuntimeReachable);
            Assert.False(shared.IsDevDependency);
            Assert.True(shared.IsDirectReference);
            Assert.Equal(Component.ComponentScope.Required, shared.Scope);
            Assert.Contains(new KeyValuePair<string, string>("Child", "1.0.0"), shared.GetDependencyEdges());
            Assert.Contains(new KeyValuePair<string, string>("Child", "2.0.0"), shared.GetDependencyEdges());
            var project = packages.Single(p => p.DependencyType == DependencyType.Project);
            Assert.Contains(new KeyValuePair<string, string>("Child", "1.0.0"), project.GetDependencyEdges());
            Assert.Contains(new KeyValuePair<string, string>("Child", "2.0.0"), project.GetDependencyEdges());
        }

        [Fact]
        public async Task RecursivelyGetProjectDotnetDependencys_WithoutAssetsFile_DoesNotWriteRecursiveWarning()
        {
            // Arrange – root project has no project.assets.json (e.g. packages.config style).
            // No warning should be written.
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Project\Project.csproj"), new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk"" />") },
                { XFS.Path(@"c:\Project\packages.config"), new MockFileData("") },
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string> { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            mockPackageFileService
                .Setup(s => s.GetDotnetDependencysAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var originalError = Console.Error;
            using var capturedError = new StringWriter();
            Console.SetError(capturedError);
            try
            {
                await projectFileService.RecursivelyGetProjectDotnetDependencysAsync(
                    XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(true);
            }
            finally
            {
                Console.SetError(originalError);
            }

            Assert.DoesNotContain("Consider removing --recursive", capturedError.ToString());
        }

        [Fact]
        public async Task GetProjectDotnetDependencys_WithConfiguration_ForwardsConfigurationToRestore()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Project\Project.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />" },
                { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
            });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                .Returns(new DotnetUtilsResult<string> { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<DotnetDependency>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            await projectFileService.GetProjectDotnetDependencysAsync(
                XFS.Path(@"c:\Project\Project.csproj"), "", false, null, null, "Release").ConfigureAwait(true);

            mockDotnetUtilsService.Verify(
                s => s.Restore(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    "Release"),
                Times.Once);
        }

        // -----------------------------------------------------------------------
        // GetAssemblyNameAndVersion — version property chain
        // -----------------------------------------------------------------------

        private ProjectFileService CreateProjectFileService(MockFileSystem mockFileSystem)
        {
            return new ProjectFileService(
                mockFileSystem,
                new Mock<IDotnetUtilsService>().Object,
                new Mock<IPackagesFileService>().Object,
                new Mock<IProjectAssetsFileService>().Object);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_ReturnsVersionElement()
        {
            var csproj = "<Project><PropertyGroup><Version>1.2.3</Version></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("1.2.3", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_FallsBackToAssemblyVersion()
        {
            var csproj = "<Project><PropertyGroup><AssemblyVersion>2.3.4</AssemblyVersion></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("2.3.4", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_FallsBackToProjectVersion()
        {
            var csproj = "<Project><PropertyGroup><ProjectVersion>3.4.5</ProjectVersion></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("3.4.5", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_FallsBackToPackageVersion()
        {
            var csproj = "<Project><PropertyGroup><PackageVersion>4.5.6</PackageVersion></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("4.5.6", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_VersionTakesPriorityOverAssemblyVersion()
        {
            var csproj = "<Project><PropertyGroup><Version>1.0.0</Version><AssemblyVersion>9.9.9</AssemblyVersion></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("1.0.0", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_AssemblyVersionTakesPriorityOverProjectVersion()
        {
            var csproj = "<Project><PropertyGroup><AssemblyVersion>2.0.0</AssemblyVersion><ProjectVersion>9.9.9</ProjectVersion></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("2.0.0", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_ReturnsNullVersionWhenNoVersionProperty()
        {
            var csproj = "<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Null(version);
        }

        /// <summary>
        /// Classic .NET Framework .csproj files declare a default XML namespace on the root
        /// &lt;Project&gt; element:
        ///   xmlns="http://schemas.microsoft.com/developer/msbuild/2003"
        ///
        /// When that namespace is present every element — including &lt;Project&gt; itself — lives
        /// in the msbuild namespace.  The XPath query must therefore prefix *all* path segments
        /// with the namespace alias, i.e.:
        ///   /msbuild:Project/msbuild:PropertyGroup/msbuild:AssemblyName
        ///
        /// Without PR #1051 only the mixed forms
        ///   /Project/PropertyGroup/AssemblyName
        ///   /Project/PropertyGroup/msbuild:AssemblyName
        /// were tried; both fail when the root &lt;Project&gt; element also carries the namespace,
        /// because its local name is qualified and the bare /Project selector won't match.
        /// The code therefore fell back to the filename, which is wrong when the
        /// &lt;AssemblyName&gt; element holds a different value.
        /// </summary>
        [Fact]
        public void GetAssemblyNameAndVersion_ClassicNetFrameworkProject_ReturnsAssemblyNameFromXmlWithDefaultNamespace()
        {
            // The <AssemblyName> inside the XML is intentionally different from the
            // .csproj filename so that the filename-fallback path produces a different
            // value, making the test sensitive to whether the XPath query works or not.
            //
            // Classic .NET Framework .csproj – ALL elements are in the msbuild namespace
            // because of the xmlns on <Project>.
            const string csproj = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""12.0"" DefaultTargets=""Build""
         xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <AssemblyName>MyClassicLibrary</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>";

            // The file is stored under a DIFFERENT name than the AssemblyName value.
            // Without the fix the XPath returns null and the code falls back to
            // "ClassicProject" (the filename stem).  With the fix it correctly returns
            // "MyClassicLibrary" from the XML.
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\ClassicProject.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (name, _) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\ClassicProject.csproj"));

            Assert.Equal("MyClassicLibrary", name);
        }

        /// <summary>
        /// <AssemblyName>$(RootNamespace)</AssemblyName> should resolve to the project's own
        /// <RootNamespace> element using best-effort substitution — not be returned as
        /// the raw, unresolved "$(RootNamespace)" literal (issue #917).
        /// </summary>
        [Theory]
        [InlineData("Company.Widgets")]
        [InlineData("Company.$(MSBuildProjectName)")]
        public void GetAssemblyNameAndVersion_ResolvesRootNamespaceToken(string rootNamespace)
        {
            string csproj = $@"<Project><PropertyGroup>
    <RootNamespace>{rootNamespace}</RootNamespace>
    <AssemblyName>$(RootNamespace)</AssemblyName>
</PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\Widgets.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (name, _) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\Widgets.csproj"));

            Assert.Equal("Company.Widgets", name);
        }

        /// <summary>
        /// When <RootNamespace> is itself unresolvable — e.g. the documented workaround
        /// "&lt;RootNamespace&gt;$(AssemblyName)&lt;/RootNamespace&gt;", which would resolve back
        /// into itself — fall back to the project's file name, same as the existing
        /// $(MSBuildProjectName) handling, instead of looping or keeping the raw token (which is
        /// what caused issue #917's duplicate-key crash).
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("<RootNamespace />")]
        [InlineData("<RootNamespace>$(AssemblyName)</RootNamespace>")]
        public void GetAssemblyNameAndVersion_RootNamespaceTokenWithoutUsableRootNamespace_FallsBackToFileName(string rootNamespaceElement)
        {
            string csproj = $@"<Project><PropertyGroup>
    {rootNamespaceElement}
    <AssemblyName>$(RootNamespace)</AssemblyName>
</PropertyGroup></Project>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\Widgets.csproj"), new MockFileData(csproj) }
            });
            var svc = CreateProjectFileService(mockFileSystem);

            var (name, _) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\Widgets.csproj"));

            Assert.Equal("Widgets", name);
        }

        // -----------------------------------------------------------------------
        // MSBuild evaluation — properties from Directory.Build.props and other imports
        // -----------------------------------------------------------------------

        private static Mock<IProjectEvaluationService> CreateProjectEvaluationService(Func<string, DotnetUtilsResult<EvaluatedProject>> evaluate)
        {
            var evaluation = new Mock<IProjectEvaluationService>();
            evaluation
                .Setup(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((IEnumerable<string> paths, IEnumerable<string> _, IEnumerable<string> _, string _, string _) =>
                    paths.ToDictionary(path => path, evaluate, StringComparer.OrdinalIgnoreCase));
            return evaluation;
        }

        private static DotnetUtilsResult<EvaluatedProject> Evaluated(Dictionary<string, string> properties, params string[] packageReferences)
        {
            var project = new EvaluatedProject();
            foreach (var property in properties)
            {
                project.Properties[property.Key] = property.Value;
            }
            project.Items["PackageReference"] = packageReferences;
            return new DotnetUtilsResult<EvaluatedProject> { Result = project };
        }

        private static (ProjectFileService service, Mock<IProjectEvaluationService> evaluation) CreateProjectFileServiceWithEvaluatedProperties(
            string csproj, Dictionary<string, string> evaluatedProperties, params string[] packageReferences)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            });
            var evaluation = CreateProjectEvaluationService(_ => evaluatedProperties is null
                ? new DotnetUtilsResult<EvaluatedProject> { ErrorMessage = "MSB4025: The project file could not be loaded." }
                : Evaluated(evaluatedProperties, packageReferences));
            var service = new ProjectFileService(
                mockFileSystem,
                new Mock<IDotnetUtilsService>().Object,
                new Mock<IPackagesFileService>().Object,
                new Mock<IProjectAssetsFileService>().Object,
                evaluation.Object);
            return (service, evaluation);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_UsesMsbuildEvaluatedVersionAndPackageId()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup><Version>$(Major).5.6</Version></PropertyGroup></Project>",
                new Dictionary<string, string>
                {
                    ["Version"] = "4.5.6",
                    ["PackageId"] = "Contoso.Package",
                    ["AssemblyName"] = "Contoso.Assembly",
                    ["IsTestProject"] = ""
                });

            var (name, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("Contoso.Package", name);
            Assert.Equal("4.5.6", version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_EvaluatedPackageIdMissing_UsesEvaluatedAssemblyName()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["Version"] = "4.5.6", ["PackageId"] = "", ["AssemblyName"] = "Contoso.Assembly" });

            var (name, _) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("Contoso.Assembly", name);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_EvaluatedSdkDefaultVersion_KeepsProjectFileFallback()
        {
            // SDK-style projects always evaluate Version to 1.0.0 when nothing sets it, so 1.0.0
            // must not override the existing fallbacks (AssemblyInfo, tool default 0.0.0).
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["Version"] = "1.0.0", ["PackageId"] = "P", ["AssemblyName"] = "P" });

            var (_, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Null(version);
        }

        [Fact]
        public void GetAssemblyNameAndVersion_EvaluationFails_FallsBackToProjectFile()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup><AssemblyName>FromXml</AssemblyName><Version>1.2.3</Version></PropertyGroup></Project>",
                null);

            var (name, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("FromXml", name);
            Assert.Equal("1.2.3", version);
        }

        [Fact]
        public void IsTestProject_UsesMsbuildEvaluatedIsTestProject()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["IsTestProject"] = "true" });

            Assert.True(svc.IsTestProject(XFS.Path(@"c:\P\P.csproj")));
        }

        [Fact]
        public void IsTestProject_TestSdkFromImports_IsDetectedThroughTheEvaluatedItems()
        {
            // e.g. Microsoft.NET.Test.Sdk referenced in Directory.Build.props: before the first restore
            // its props (which set IsTestProject) are not imported yet, but the item is evaluated
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["IsTestProject"] = "" },
                "Microsoft.NET.Test.Sdk");

            Assert.True(svc.IsTestProject(XFS.Path(@"c:\P\P.csproj")));
        }

        [Fact]
        public void IsTestProject_EvaluationFails_FallsBackToProjectFile()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>",
                null);

            Assert.True(svc.IsTestProject(XFS.Path(@"c:\P\P.csproj")));
        }

        [Fact]
        public void EvaluatedProperties_AreRequestedOncePerProject()
        {
            var (svc, evaluation) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["Version"] = "4.5.6", ["IsTestProject"] = "" });

            svc.IsTestProject(XFS.Path(@"c:\P\P.csproj"));
            svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));
            svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            evaluation.Verify(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void EvaluatedProperties_UseEvaluationFrameworkAndConfiguration()
        {
            var (svc, evaluation) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup /></Project>",
                new Dictionary<string, string> { ["Version"] = "2.0.0" });
            svc.EvaluationFramework = "net8.0";
            svc.EvaluationConfiguration = "Release";

            svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            evaluation.Verify(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), "net8.0", "Release"), Times.Once);
        }

        // -----------------------------------------------------------------------
        // Batches — every project found so far is evaluated in the same dotnet process
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProjectAndItsReferences_AreEvaluatedInOneBatch()
        {
            var projectA = XFS.Path(@"c:\S\A\A.csproj");
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { projectA, Helpers.GetProjectFileWithProjectReferences(new[] { @"..\B\B.csproj", @"..\C\C.csproj" }) },
                { XFS.Path(@"c:\S\B\B.csproj"), Helpers.GetEmptyProjectFile() },
                { XFS.Path(@"c:\S\C\C.csproj"), Helpers.GetEmptyProjectFile() }
            });
            var evaluation = CreateProjectEvaluationService(path => Evaluated(new Dictionary<string, string> { ["Version"] = "2.0.0" }));
            var svc = new ProjectFileService(mockFileSystem, new Mock<IDotnetUtilsService>().Object, new Mock<IPackagesFileService>().Object, new Mock<IProjectAssetsFileService>().Object, evaluation.Object);

            var references = await svc.GetProjectReferencesAsync(projectA).ConfigureAwait(true);
            svc.GetAssemblyNameAndVersion(projectA);
            foreach (var reference in references)
            {
                svc.GetAssemblyNameAndVersion(reference);
            }

            evaluation.Verify(m => m.EvaluateProjects(
                It.Is<IEnumerable<string>>(paths => paths.Count() == 3), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            evaluation.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProjectReferencesOfAllLevels_AreEvaluatedInOneBatch()
        {
            // A -> B -> C -> D: every level used to start its own MSBuild evaluation
            var projectA = XFS.Path(@"c:\S\A\A.csproj");
            var projectB = XFS.Path(@"c:\S\B\B.csproj");
            var projectC = XFS.Path(@"c:\S\C\C.csproj");
            var projectD = XFS.Path(@"c:\S\D\D.csproj");
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { projectA, Helpers.GetProjectFileWithProjectReferences(new[] { @"..\B\B.csproj" }) },
                { projectB, Helpers.GetProjectFileWithProjectReferences(new[] { @"..\C\C.csproj" }) },
                { projectC, Helpers.GetProjectFileWithProjectReferences(new[] { @"..\D\D.csproj" }) },
                { projectD, Helpers.GetEmptyProjectFile() }
            });
            var evaluation = CreateProjectEvaluationService(path => Evaluated(new Dictionary<string, string> { ["Version"] = "2.0.0" }));
            var svc = new ProjectFileService(mockFileSystem, new Mock<IDotnetUtilsService>().Object, new Mock<IPackagesFileService>().Object, new Mock<IProjectAssetsFileService>().Object, evaluation.Object);

            var projects = await svc.RecursivelyGetProjectReferencesAsync(projectA).ConfigureAwait(true);

            Assert.Equal(new[] { "A@2.0.0", "B@2.0.0", "C@2.0.0", "D@2.0.0" }, projects.Select(p => $"{p.Name}@{p.Version}").OrderBy(p => p));
            evaluation.Verify(m => m.EvaluateProjects(
                It.Is<IEnumerable<string>>(paths => paths.OrderBy(p => p).SequenceEqual(new[] { projectA, projectB, projectC, projectD })),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            evaluation.VerifyNoOtherCalls();
        }

        [Fact]
        public void QueuedProjects_AreEvaluatedInOneBatch()
        {
            var projectX = XFS.Path(@"c:\S\X\X.csproj");
            var projectY = XFS.Path(@"c:\S\Y\Y.csproj");
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { projectX, Helpers.GetEmptyProjectFile() },
                { projectY, Helpers.GetEmptyProjectFile() }
            });
            var evaluation = CreateProjectEvaluationService(path => Evaluated(new Dictionary<string, string> { ["IsTestProject"] = "" }));
            var svc = new ProjectFileService(mockFileSystem, new Mock<IDotnetUtilsService>().Object, new Mock<IPackagesFileService>().Object, new Mock<IProjectAssetsFileService>().Object, evaluation.Object);

            svc.QueueEvaluation(new[] { projectX, projectY });
            svc.IsTestProject(projectX);
            svc.IsTestProject(projectY);

            evaluation.Verify(m => m.EvaluateProjects(
                It.Is<IEnumerable<string>>(paths => paths.Count() == 2), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            evaluation.VerifyNoOtherCalls();
        }

        // -----------------------------------------------------------------------
        // Project file XML — the fallback when a project cannot be evaluated
        // -----------------------------------------------------------------------

        [Fact]
        public void GetAssemblyNameAndVersion_ClassicProjectWithMsbuildNamespace_ReadsDeclaredVersion()
        {
            const string csproj = @"<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup><AssemblyName>Classic</AssemblyName><Version>1.2.3</Version></PropertyGroup>
</Project>";
            var svc = CreateProjectFileService(new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData(csproj) }
            }));

            var (name, version) = svc.GetAssemblyNameAndVersion(XFS.Path(@"c:\P\P.csproj"));

            Assert.Equal("Classic", name);
            Assert.Equal("1.2.3", version);
        }

        [Theory]
        [InlineData("<ItemGroup><PackageReference Include=\"microsoft.net.test.sdk\" /></ItemGroup>")]
        [InlineData("<Choose><When Condition=\"true\"><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup></When></Choose>")]
        public void IsTestProject_DetectsTestSdkReferenceInAnyItemGroup(string items)
        {
            var svc = CreateProjectFileService(new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\P\P.csproj"), new MockFileData($"<Project>{items}</Project>") }
            }));

            Assert.True(svc.IsTestProject(XFS.Path(@"c:\P\P.csproj")));
        }

        [Fact]
        public void IsTestProject_EvaluatedValue_WinsOverConditionalDeclaration()
        {
            var (svc, _) = CreateProjectFileServiceWithEvaluatedProperties(
                "<Project><PropertyGroup Condition=\"'$(Configuration)' == 'Never'\"><IsTestProject>true</IsTestProject></PropertyGroup></Project>",
                new Dictionary<string, string> { ["IsTestProject"] = "false" });

            Assert.False(svc.IsTestProject(XFS.Path(@"c:\P\P.csproj")));
        }

        [Fact]
        public void GetProjectAssetsFilePath_UsesEvaluatedProjectAssetsFile_InsteadOfDotnetCli()
        {
            var projectPath = XFS.Path(@"c:\P\P.csproj");
            var assetsPath = XFS.Path(@"c:\artifacts\obj\P\project.assets.json");
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { projectPath, new MockFileData("<Project />") },
                { assetsPath, new MockFileData("{}") }
            });
            var dotnetUtils = new Mock<IDotnetUtilsService>();
            var evaluation = CreateProjectEvaluationService(_ => Evaluated(new Dictionary<string, string> { ["ProjectAssetsFile"] = assetsPath }));
            var svc = new ProjectFileService(mockFileSystem, dotnetUtils.Object, new Mock<IPackagesFileService>().Object, new Mock<IProjectAssetsFileService>().Object, evaluation.Object);

            Assert.Equal(assetsPath, svc.GetProjectAssetsFilePath(projectPath, ""));
            dotnetUtils.Verify(m => m.GetAssetsPath(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetProjectReferences_ReadsTheProjectFileWithoutEvaluatingIt()
        {
            var projectPath = XFS.Path(@"c:\P\P.csproj");
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { projectPath, new MockFileData(@"<Project><ItemGroup><ProjectReference Include=""..\Lib\Lib.csproj"" /></ItemGroup></Project>") }
            });
            var evaluation = new Mock<IProjectEvaluationService>();
            var svc = new ProjectFileService(mockFileSystem, new Mock<IDotnetUtilsService>().Object, new Mock<IPackagesFileService>().Object, new Mock<IProjectAssetsFileService>().Object, evaluation.Object);

            var references = await svc.GetProjectReferencesAsync(projectPath).ConfigureAwait(true);

            Assert.Equal(XFS.Path(@"c:\Lib\Lib.csproj"), Assert.Single(references));
            evaluation.VerifyNoOtherCalls();
        }
    }
}
