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

namespace CycloneDX.Tests
{
    public class ProjectFileServiceTests
    {
        [Theory]
        [InlineData(@"C:\github\cyclonedx-dotnet\Core\CycloneDX.csproj", "", @"C:\github\cyclonedx-dotnet\Core\obj")]
        [InlineData(@"C:\github\cyclonedx-dotnet\Core\CycloneDX.csproj", @"C:\github\cyclonedx-dotnet\artifacts", @"C:\github\cyclonedx-dotnet\artifacts\obj\CycloneDX")]
        public void GetPropertyUseProjectFileName(string projectFilePath, string baseIntermediateOutputPath, string expected)
        {
          string outputPath = ProjectFileService.GetProjectProperty(XFS.Path(projectFilePath), XFS.Path(baseIntermediateOutputPath));
          Assert.Equal(XFS.Path(expected), outputPath);
        }

        [Fact]
        public void IsTestProjectTrue()
        {
            string szProjectPath = System.Environment.CurrentDirectory + XFS.Path(@"\..\..\..\..\CycloneDX.Tests\CycloneDX.Tests.csproj");
            Assert.True(ProjectFileService.IsTestProject(szProjectPath));
        }

        [Fact]
        public void IsTestProjectFalse()
        {
            string szProjectPath = System.Environment.CurrentDirectory + XFS.Path(@"\..\..\..\..\CycloneDX\CycloneDX.csproj");
            Assert.False(ProjectFileService.IsTestProject(szProjectPath));
        }

        [Fact]
        public async Task GetProjectNugetPackages_WithProjectAssetsFile_ReturnsNugetPackage()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<NugetPackage>
                {
                    new NugetPackage { Name = "Package", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(false);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectNugetPackages_WithProjectAssetsFileWithoutRestore_ReturnsNugetPackage()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ApplicationException("Restore should not be called"));
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<NugetPackage>
                {
                    new NugetPackage { Name = "Package", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);
            projectFileService.DisablePackageRestore = true;

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(false);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectNugetPackages_WithProjectAssetsFile_ReturnsMultipleNugetPackages()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "" },
                    { XFS.Path(@"c:\Project\obj\project.assets.json"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<NugetPackage>
                {
                    new NugetPackage { Name = "Package1", Version = "1.2.3" },
                    new NugetPackage { Name = "Package2", Version = "1.2.3" },
                    new NugetPackage { Name = "Package3", Version = "1.2.3" },
                });
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(false);
            var sortedPackages = new List<NugetPackage>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name));
        }

        [Fact]
        public async Task GetProjectNugetPackages_WithPackagesConfig_ReturnsNugetPackage()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "" },
                    { XFS.Path(@"c:\Project\packages.config"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            var mockPackageFileService = new Mock<IPackagesFileService>();
            mockPackageFileService
                .Setup(s => s.GetNugetPackagesAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    new HashSet<NugetPackage>
                    {
                        new NugetPackage { Name = "Package", Version = "1.2.3" },
                    }
                );
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<NugetPackage>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(false);

            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectNugetPackages_WithPackagesConfig_ReturnsMultipleNugetPackages()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), "" },
                    { XFS.Path(@"c:\Project\packages.config"), "" },
                });
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            var mockPackageFileService = new Mock<IPackagesFileService>();
            mockPackageFileService
                .Setup(s => s.GetNugetPackagesAsync(It.IsAny<string>()))
                .ReturnsAsync(
                    new HashSet<NugetPackage>
                    {
                    new NugetPackage { Name = "Package1", Version = "1.2.3" },
                    new NugetPackage { Name = "Package2", Version = "1.2.3" },
                    new NugetPackage { Name = "Package3", Version = "1.2.3" },
                    }
                );
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();
            mockProjectAssetsFileService
                .Setup(s => s.GetNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new HashSet<NugetPackage>());
            var projectFileService = new ProjectFileService(
                mockFileSystem,
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"), "", false, "", "").ConfigureAwait(false);
            var sortedPackages = new List<NugetPackage>(packages);
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

            var projects = await projectFileService.GetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project\Project.csproj")).ConfigureAwait(false);
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

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj")).ConfigureAwait(false);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();

            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }
    }
}
