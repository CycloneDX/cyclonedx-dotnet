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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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

    }
}
