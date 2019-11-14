// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class ProjectFileServiceTests
    {
        [Fact]
        public async Task GetProjectNugetPackages_ReturnsNugetPackage()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), Helpers.GetProjectFileWithPackageReference("Package", "1.2.3") },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"));
            
            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectNugetPackages_ReturnsMultipleNugetPackages()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new List<NugetPackage> {
                            new NugetPackage { Name = "Package1", Version = "1.2.3"},
                            new NugetPackage { Name = "Package2", Version = "1.2.3"},
                            new NugetPackage { Name = "Package3", Version = "1.2.3"},
                        })
                    },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"));
            var sortedPackages = new List<NugetPackage>(packages);
            sortedPackages.Sort();
            
            Assert.Collection(sortedPackages,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name));
        }

        [Fact]
        public async Task GetProjectNugetPackages_IgnoresImplicitPackageVersions()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), Helpers.GetProjectFileWithPackageReference("Package", "") },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"));
            
            Assert.Empty(packages);
        }

        [Fact]
        public async Task GetProjectNugetPackages_ReturnsPackagesConfigReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Project.csproj"), new MockFileData(@"<Project></Project>") },
                    { XFS.Path(@"c:\Project\packages.config"), Helpers.GetPackagesFileWithPackageReference("Package", "1.2.3") },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var packages = await projectFileService.GetProjectNugetPackagesAsync(XFS.Path(@"c:\Project\Project.csproj"));
            
            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetProjectReferences_ReturnsProjectReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new string[] {
                            @"..\Project1\Project1.csproj",
                            @"..\Project2\Project2.csproj",
                            @"..\Project3\Project3.csproj",
                        })
                    },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var projects = await projectFileService.GetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project\Project.csproj"));
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
                        new string[] {
                            @"..\Project2\Project2.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new string[] {
                            @"..\Project3\Project3.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), new MockFileData(@"<Project></Project>") },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var projects = await projectFileService.RecursivelyGetProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"));
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        [Fact]
        public async Task RecursivelyGetProjectNugetPackages_ReturnsNugetPackages()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithReferences(
                        new string[] {
                            @"..\Project2\Project2.csproj",
                            @"..\Project3\Project3.csproj",
                        },
                        new NugetPackage[] {
                            new NugetPackage { Name = "Package1", Version = "1.2.3" },
                            new NugetPackage { Name = "Package2", Version = "1.2.3" },
                            new NugetPackage { Name = "Package3", Version = "1.2.3" },
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetProjectFileWithReferences(
                        new string[] {
                            @"..\Project3\Project3.csproj",
                        },
                        new NugetPackage[] {
                            new NugetPackage { Name = "Package1", Version = "1.2.4" },
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), Helpers.GetProjectFileWithReferences(
                        null,
                        new NugetPackage[] {
                            new NugetPackage { Name = "Package1", Version = "1.2.3" },
                        })
                    },
                });
            var projectFileService = new ProjectFileService(mockFileSystem);

            var packages = await projectFileService.RecursivelyGetProjectNugetPackagesAsync(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"));
            var sortedPackages = new List<NugetPackage>(packages);
            sortedPackages.Sort();
            
            Assert.Collection(sortedPackages,
                item => {
                    Assert.Equal("Package1", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                },
                item => {
                    Assert.Equal("Package1", item.Name);
                    Assert.Equal("1.2.4", item.Version);
                },
                item => {
                    Assert.Equal("Package2", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                },
                item => {
                    Assert.Equal("Package3", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                }
            );
        }
    }
}
