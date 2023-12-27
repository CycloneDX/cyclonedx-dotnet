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
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.IO;
using NuGet.Packaging.Signing;

namespace CycloneDX.Tests
{
    public class ProjectAssetsFileServiceTests
    {

        [Theory]
        [InlineData(".NETStandard", 2, 1, ".NETStandard,Version=v2.1")]
        [InlineData(".NETCoreApp", 6, 0, "net6.0")]
        public void GetDotnetDependencys_PackageAsTopLevelAndTransitive(string framework, int frameworkMajor, int frameworkMinor, string projectFileDependencyGroupsName)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new[] {
                            new DotnetDependency
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                                Dependencies = new Dictionary<string, string>
                                {
                                    { "Package2", "[4.5, )" },
                                },
                            },
                            new DotnetDependency
                            {
                                Name = "Package2",
                                Version = "4.5.1",
                                Dependencies = new Dictionary<string, string>(),
                            },
                            new DotnetDependency
                            {
                                Name = "Package3",
                                Version = "1.0.0",
                                Dependencies = new Dictionary<string, string>(),
                                IsDevDependency = true
                            }
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), new MockFileData("")
                    }
                });

            var mockAssetReader = new Mock<IAssetFileReader>();
            mockAssetReader
                .Setup(m => m.Read(It.IsAny<Stream>(), It.IsAny<string>()))
                .Returns(() =>
                {
                    var nuGetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0));
                    return new LockFile
                    {
                        Targets = new[]
                        {
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
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
                                TargetFramework = nuGetFramework,
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
                        },
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies = new List<LibraryDependency>
                                    {
                                        new()
                                        {
                                            SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent,
                                            ReferenceType = LibraryDependencyReferenceType.Direct,
                                            LibraryRange = new LibraryRange
                                            {
                                                Name = "Package1",
                                                VersionRange = VersionRange.Parse("1.5.0"),
                                                TypeConstraint = LibraryDependencyTarget.Package
                                            }
                                        },
                                        new()
                                        {
                                            SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent,
                                            ReferenceType = LibraryDependencyReferenceType.Direct,
                                            LibraryRange = new LibraryRange
                                            {
                                                Name = "Package2",
                                                VersionRange = VersionRange.Parse("4.5.1"),
                                                TypeConstraint = LibraryDependencyTarget.Package
                                            }
                                        },
                                        new()
                                        {
                                            SuppressParent = LibraryIncludeFlags.All,
                                            ReferenceType = LibraryDependencyReferenceType.Direct,
                                            LibraryRange = new LibraryRange
                                            {
                                                Name = "Package3",
                                                VersionRange = VersionRange.Parse("1.0.0"),
                                                TypeConstraint = LibraryDependencyTarget.Package
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        Libraries = new[]
                        {
                            new LockFileLibrary
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        },
                        ProjectFileDependencyGroups = new[]
                        {

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" }),
                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package2 >= 4.5.1" }),
                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package3 >= 1.0.0" })
                        }
                    };
                });
            mockAssetReader.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                return "empty";
            });

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, () => mockAssetReader.Object);
            var packages = projectAssetsFileService.GetDotnetDependencys(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"),  false);
            var sortedPackages = new List<DotnetDependency>(packages);

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
        [InlineData(".NETStandard", 2, 1, ".NETStandard,Version=v2.1")]
        [InlineData(".NETCoreApp", 6, 0, "net6.0")]
        public void GetDotnetDependencys_MissingResolvedPackageVersion(string framework, int frameworkMajor, int frameworkMinor, string projectFileDependencyGroupsName)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new[] {
                            new DotnetDependency
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

            var mockAssetReader = new Mock<IAssetFileReader>(MockBehavior.Strict);
            mockAssetReader
                .Setup(m => m.Read(It.IsAny<Stream>(), It.IsAny<string>()))
                .Returns(() =>
                {
                    var nuGetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor, 0));
                    return new LockFile
                    {
                        Targets = new[]
                        {
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
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
                                TargetFramework = nuGetFramework,
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
                        },
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies = new List<LibraryDependency>
                                    {
                                        new()
                                        {
                                            SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent,
                                            ReferenceType = LibraryDependencyReferenceType.Direct,
                                            LibraryRange = new LibraryRange
                                            {
                                                Name = "Package1",
                                                VersionRange = VersionRange.Parse("1.5.0"),
                                                TypeConstraint = LibraryDependencyTarget.Package
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        Libraries = new[]
                        {
                            new LockFileLibrary
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        },
                        ProjectFileDependencyGroups = new[]
                        {

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" })
                        }
                    };
                });
            mockAssetReader.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                return "empty";
            });

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, () => mockAssetReader.Object);
            var packages = projectAssetsFileService.GetDotnetDependencys(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false);
            var sortedPackages = new List<DotnetDependency>(packages);

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

        [Theory]
        [InlineData(".NETStandard", 2, 1, ".NETStandard,Version=v2.1")]
        [InlineData(".NETCoreApp", 6, 0, "net6.0")]
        public void GetDotnetDependencys_MissingDependencies(string framework, int frameworkMajor, int frameworkMinor, string projectFileDependencyGroupsName)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        new[] {
                            new DotnetDependency
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                            }
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), new MockFileData("")
                    }
                });
            var mockAssetReader = new Mock<IAssetFileReader>(MockBehavior.Strict);
            mockAssetReader
                .Setup(m => m.Read(It.IsAny<Stream>(), It.IsAny<string>()))
                .Returns(() =>
                {
                    var nuGetFramework = new NuGet.Frameworks.NuGetFramework(framework, new Version(frameworkMajor, frameworkMinor));
                        


                    return new LockFile
                    {
                        Targets = new[]
                        {
                            new LockFileTarget
                            {                               
                                TargetFramework = nuGetFramework,
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
                                        }
                                    }
                                }
                            },
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
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
                                        }
                                    }
                                }
                            }
                        },
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies = new List<LibraryDependency>
                                    {
                                        new()
                                        {
                                            SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent,
                                            ReferenceType = LibraryDependencyReferenceType.Direct,
                                            LibraryRange = new LibraryRange
                                            {
                                                Name = "Package1",
                                                VersionRange = VersionRange.Parse("1.5.0"),
                                                TypeConstraint = LibraryDependencyTarget.Package
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        Libraries = new[]
                        {
                            new LockFileLibrary()
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        },
                        ProjectFileDependencyGroups = new[]
                        {

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" })
                        }
                    };
                });
            mockAssetReader.Setup(m => m.ReadAllText(It.IsAny<string>())).Returns(() =>
            {
                return "empty";
            });

            var projectAssetsFileService = new ProjectAssetsFileService(mockFileSystem, () => mockAssetReader.Object);
            var packages = projectAssetsFileService.GetDotnetDependencys(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), XFS.Path(@"c:\SolutionPath\Project1\obj\project.assets.json"), false);
            var sortedPackages = new List<DotnetDependency>(packages);

            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item =>
                {
                    Assert.Equal(@"Package1", item.Name);
                    Assert.Equal(@"1.5.0", item.Version);
                    Assert.True(item.IsDirectReference, "Package1 was expected to be a direct reference.");
                    Assert.Empty(item.Dependencies);
                });
        }
    }
}
