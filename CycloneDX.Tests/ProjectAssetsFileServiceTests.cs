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
using CycloneDX.Interfaces;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Moq;
using CycloneDX.Models;
using CycloneDX.Services;
using NuGet.ProjectModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Text.Json;

namespace CycloneDX.Tests
{
    public class ProjectAssetsFileServiceTests
    {

        protected readonly string jsonString1 = /*lang=json,strict*/ """
{
    "version": 3,
    "libraries": {
        "Package1/1.5.0": {
            "files": [
                "Package1.dll"
            ],
            "path": "Package1/1.5.0",
            "type": "package"
        },
        "Package2/4.5.1": {
            "files": [
                "Package2.dll"
            ],
            "path": "Package2/4.5.1",
            "type": "package"
        }
    },
    "project": {
        "frameworks": {
            "net6.0": {
            "targetAlias": "net6.0",
                "dependencies": {
                    "Package1": {
                        "target": "Package",
                        "version": "[1.6.0,)"
                    }
                }
            },
            "netstandard2.1": {
            "targetAlias": "netstandard2.1",
                "dependencies": {
                    "Package1": {
                        "target": "Package",
                        "version": "[1.6.0,)"
                    }
                }
            }
        }
    }
}
""";

        protected readonly string jsonString2 = /*lang=json,strict*/ """
{
    "version": 3,
    "targets": {
       "net6.0": {
           "Package1/1.5.0": {
                "type": "package",
                "dependencies": {
                    "Package2": "4.5.1"
                    }
               },
            "Package2/4.5.1": {
                 "type": "package"
                },
            "Package3/1.0.0": {
                 "type": "package"
                }
            },
       "net6.0/win-x64": {
           "Package1/1.5.0": {
                "type": "package",
                "dependencies": {
                    "Package2": "4.5.1"
                    }
               },
            "Package2/4.5.1": {
                 "type": "package"
                },
            "Package3/1.0.0": {
                 "type": "package"
                }
            }
       },
    "libraries": {
        "Package1/1.5.0": {
            "files": [
                "Package1.dll"
            ],
            "path": "Package1/1.5.0",
            "type": "package"
        },
        "Package2/4.5.1": {
            "files": [
                "Package2.dll"
            ],
            "path": "Package2/4.5.1",
            "type": "package"
        },
        "Package3/1.0.0": {
            "files": [
                "Package3.dll"
            ],
            "path": "Package3/1.0.0",
            "type": "package"
            }
    },
    "project": {
        "frameworks": {
            "net6.0": {
            "targetAlias": "net6.0",
                "dependencies": {
                    "Package1": {
                        "target": "Package",
                        "version": "[1.5.0,)"
                    },
                    "Package2": {
                        "target": "Package",
                        "version": "[4.5.1,)"
                    },
                    "Package3": {
                        "suppressParent": "All",
                        "target": "Package",
                        "version": "[1.0.0,)"
                    }
                }
            },
            "netstandard2.1": {
            "targetAlias": "netstandard2.1",
                "dependencies": {
                    "Package1": {
                        "target": "Package",
                        "version": "[1.5.0,)"
                        },
                    "Package2": {
                        "target": "Package",
                        "version": "[4.5.1,)"
                    },
                    "Package3": {
                        "suppressParent": "All",
                        "target": "Package",
                        "version": "[1.0.0,)"
                    }
                }
            }
        }
    },
    "runtimes": {
        "win-x64": {
            "#import": []
        }
    }
}
""";

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
                            },
                            new NugetPackage
                            {
                                Name = "Package3",
                                Version = "1.0.0",
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
                            ("Package3", new[]{ ("Package3", "1.0.0") }),
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
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package3",
                                        Version = new NuGet.Versioning.NuGetVersion("1.0.0"),
                                        CompileTimeAssemblies = new[]
                                        {
                                            new LockFileItem("Package3.dll")
                                        },
                                        Dependencies = new PackageDependency[0]
                                    }
                                }
                            },
                            new LockFileTarget
                            {
                                TargetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0)),
                                RuntimeIdentifier = "win-x64",
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
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package3",
                                        Version = new NuGet.Versioning.NuGetVersion("1.0.0"),
                                        CompileTimeAssemblies = new[]
                                        {
                                            new LockFileItem("Package3.dll")
                                        },
                                        Dependencies = new PackageDependency[0]
                                    }
                                }
                            }
                        }
                    };
                });
            mockAssetReader.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                return "empty";
            });

            var mockJsonDoc = new Mock<IJsonDocs>();
            mockJsonDoc
                .Setup(m => m.Parse(It.IsAny<string>()))
                .Returns(() => JsonDocument.Parse(jsonString2)
                );

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, mockDotnetCommandsService.Object, () => mockAssetReader.Object, mockJsonDoc.Object );
            var packages = projectAssetsFileService.GetNugetPackages(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false, false);
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
                },
                item =>
                {
                    Assert.Equal(@"Package3", item.Name);
                    Assert.Equal(@"1.0.0", item.Version);
                    Assert.True(item.IsDirectReference, "Package3 was expected to be a direct reference.");
                    Assert.True(item.IsDevDependency, "Package3 was expected to be a development reference.");
                    Assert.Empty(item.Dependencies);
                }
                );
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
            var mockAssetReader = new Mock<IAssetFileReader>(MockBehavior.Strict);
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
                            },
                            new LockFileTarget
                            {
                                TargetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0)),
                                RuntimeIdentifier = "win-x64",
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
            mockAssetReader.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                return "empty";
            });
            var mockJsonDoc = new Mock<IJsonDocs>(MockBehavior.Strict);
            mockJsonDoc
                .Setup(m => m.Parse(It.IsAny<string>()))
                .Returns(() => JsonDocument.Parse(jsonString2)
                );

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, mockDotnetCommandsService.Object, () =>mockAssetReader.Object, mockJsonDoc.Object);
            var packages = projectAssetsFileService.GetNugetPackages(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false, false);
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
