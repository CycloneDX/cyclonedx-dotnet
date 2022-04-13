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
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Moq;
using CycloneDX.Models;
using CycloneDX.Services;
using NuGet.ProjectModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace CycloneDX.Tests
{
    public class ProjectAssetsFileServiceTests
    {
        [Theory]
        [InlineData(".NetStandard", 2, 1)]
        [InlineData("net", 6, 0)]
        public void GetNugetPackages_PackageAsTopLevelAndTransitive(string framework, int frameworkMajor, int frameworkMinor)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new[] {
                            new NugetPackage
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                                Dependencies = new Dictionary<string, string>
                                {
                                    { "Package2", "[4.5, )" },
                                },
                            },
                            new NugetPackage
                            {
                                Name = "Package2",
                                Version = "4.5.1",
                                Dependencies = new Dictionary<string, string>(),
                            }
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), new MockFileData("")
                    }
                });
            var mockDotnetCommandsService = new Mock<IDotnetCommandService>();
            mockDotnetCommandsService.Setup(m => m.Run(It.IsAny<string>()))
                .Returns(() => Helpers.GetDotnetListPackagesResult(
                        new[]
                        {
                            ("Package1", new[]{ ("Package1", "1.5.0") }),
                            ("Package2", new[]{ ("Package2", "4.5.1") }),
                        }));
            var mockAssetReader = new Mock<IAssetFileReader>();
            mockAssetReader
                .Setup(m => m.Read(It.IsAny<string>()))
                .Returns(() =>
                {
                    return new LockFile
                    {
                        Targets = new[]
                        {
                            new LockFileTarget
                            {
                                TargetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0)),
                                RuntimeIdentifier = "",
                                Libraries = new[]
                                {
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies = new[]
                                        {
                                            new LockFileItem("Package1.dll")
                                        },
                                        Dependencies = new[]
                                        {
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        }
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package2",
                                        Version = new NuGet.Versioning.NuGetVersion("4.5.1"),
                                        CompileTimeAssemblies = new[]
                                        {
                                            new LockFileItem("Package2.dll")
                                        },
                                        Dependencies = new PackageDependency[0]
                                    }
                                }
                            }
                        }
                    };
                });

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, mockDotnetCommandsService.Object, () => mockAssetReader.Object);
            var packages = projectAssetsFileService.GetNugetPackages(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false);
            var sortedPackages = new List<NugetPackage>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item =>
                {
                    Assert.Equal(@"Package1", item.Name);
                    Assert.Equal(@"1.5.0", item.Version);
                    Assert.True(item.IsDirectReference, "Package1 was expected to be a direct reference.");
                    Assert.Collection(item.Dependencies,
                        dep =>
                        {
                            Assert.Equal(@"Package2", dep.Key);
                            Assert.Equal(@"4.5.1", dep.Value);
                        });
                },
                item =>
                {
                    Assert.Equal(@"Package2", item.Name);
                    Assert.Equal(@"4.5.1", item.Version);
                    Assert.True(item.IsDirectReference, "Package2 was expected to be a direct reference.");
                    Assert.Empty(item.Dependencies);
                });
        }

        [Theory]
        [InlineData(".NetStandard", 2, 1)]
        [InlineData("net", 6, 0)]
        public void GetNugetPackages_MissingResolvedPackageVersion(string framework, int frameworkMajor, int frameworkMinor)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new[] {
                            new NugetPackage
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                                Dependencies = new Dictionary<string, string>
                                {
                                    { "Package2", "[4.5, )" },
                                },
                            }
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), new MockFileData("")
                    }
                });
            var mockDotnetCommandsService = new Mock<IDotnetCommandService>();
            mockDotnetCommandsService.Setup(m => m.Run(It.IsAny<string>()))
                .Returns(() => Helpers.GetDotnetListPackagesResult(
                        new[]
                        {
                            ("Package1", new[]{ ("Package1", "1.5.0") }),
                        }));
            var mockAssetReader = new Mock<IAssetFileReader>();
            mockAssetReader
                .Setup(m => m.Read(It.IsAny<string>()))
                .Returns(() =>
                {
                    return new LockFile
                    {
                        Targets = new[]
                        {
                            new LockFileTarget
                            {
                                TargetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0)),
                                RuntimeIdentifier = "",
                                Libraries = new[]
                                {
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies = new[]
                                        {
                                            new LockFileItem("Package1.dll")
                                        },
                                        Dependencies = new[]
                                        {
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        }
                                    }
                                }
                            }
                        }
                    };
                });

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, mockDotnetCommandsService.Object, () => mockAssetReader.Object);
            var packages = projectAssetsFileService.GetNugetPackages(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false);
            var sortedPackages = new List<NugetPackage>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item =>
                {
                    Assert.Equal(@"Package1", item.Name);
                    Assert.Equal(@"1.5.0", item.Version);
                    Assert.True(item.IsDirectReference, "Package1 was expected to be a direct reference.");
                    Assert.Collection(item.Dependencies,
                        dep =>
                        {
                            Assert.Equal(@"Package2", dep.Key);
                            Assert.Equal(@"[4.5.0, )", dep.Value);
                        });
                });
        }
    }
}
