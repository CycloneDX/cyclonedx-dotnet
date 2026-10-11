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
using System.Collections.Immutable;
using System.Linq;

namespace CycloneDX.Tests
{
    public class ProjectAssetsFileServiceTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetDotnetDependencys_LocalVersionOutsideRangeRetainsProvenanceAndCompatibilityScope(bool isTestProject)
        {
            const string assetsJson = """
                {
                  "version": 3,
                  "targets": {
                    "net10.0": {
                      "Consumer/1.0.0": {
                        "type": "package",
                        "dependencies": { "Shared": "[1.0.0]" },
                        "runtime": { "lib/net10.0/Consumer.dll": {} }
                      },
                      "Shared/2.0.0": {
                        "type": "package",
                        "runtime": { "lib/net10.0/Shared.dll": {} }
                      }
                    }
                  },
                  "libraries": {
                    "Consumer/1.0.0": { "type": "package", "path": "consumer/1.0.0", "files": [] },
                    "Shared/2.0.0": { "type": "package", "path": "shared/2.0.0", "files": [] }
                  },
                  "projectFileDependencyGroups": { "net10.0": ["Consumer >= 1.0.0"] },
                  "project": {
                    "version": "1.0.0",
                    "frameworks": {
                      "net10.0": {
                        "targetAlias": "net10.0",
                        "dependencies": { "Consumer": { "target": "Package", "version": "[1.0.0, )" } }
                      }
                    }
                  }
                }
                """;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/project/obj/project.assets.json"] = assetsJson });
            var packages = new ProjectAssetsFileService(fileSystem, () => new AssetFileReader())
                .GetDotnetDependencys("/project/project.csproj", "/project/obj/project.assets.json", isTestProject);
            var consumer = packages.Single(p => p.Name == "Consumer");
            var unresolvedEdge = new KeyValuePair<string, string>("Shared", "[1.0.0, 1.0.0]");
            Assert.Equal(unresolvedEdge, Assert.Single(consumer.NonDevelopmentUnresolvedDependencyEdges));
            if (isTestProject)
                Assert.Empty(consumer.RuntimeUnresolvedDependencyEdges);
            else
                Assert.Equal(unresolvedEdge, Assert.Single(consumer.RuntimeUnresolvedDependencyEdges));
            // Keep the historical single-name fallback for scope/graph compatibility;
            // it must not erase evidence that the requested range was unsatisfied.
            var shared = packages.Single(p => p.Name == "Shared");
            Assert.Equal("2.0.0", shared.Version);
            Assert.True(shared.IsNonDevelopmentReachable);
            Assert.Equal(!isTestProject, shared.IsRuntimeReachable);
            Assert.Equal(isTestProject ? Component.ComponentScope.Excluded : Component.ComponentScope.Required, shared.Scope);
            Assert.Equal(unresolvedEdge.Value, consumer.Dependencies["Shared"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SetIsDevDependency_RealBuildAssetsWithPlaceholderRuntime_AreDevelopmentOnly(bool analyzer)
        {
            var reference = new LibraryDependency { LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package) };
            var target = new LockFileTargetLibrary
            {
                Build = analyzer ? [] : [new LockFileItem("build/Package.targets")],
                CompileTimeAssemblies = [new LockFileItem("ref/net8.0/_._")],
                RuntimeAssemblies = [new LockFileItem("lib/net8.0/_._")],
                NativeLibraries = [new LockFileItem("runtimes/linux-x64/native/_._")],
                ResourceAssemblies = [new LockFileItem("lib/net8.0/en/_._")],
                EmbedAssemblies = [new LockFileItem("embed/_._")]
            };
            var library = new LockFileLibrary { Files = analyzer ? ["analyzers/dotnet/cs/Package.dll"] : [] };
            Assert.True(new ProjectAssetsFileService(new MockFileSystem(), null).SetIsDevDependency(reference, target, library));
        }

        [Fact]
        public void SetIsDevDependency_PlaceholderOnlyAssets_AreNotPositiveEvidenceOfDevelopmentUse()
        {
            var target = new LockFileTargetLibrary
            {
                Build = [new LockFileItem("build/_._")],
                BuildMultiTargeting = [new LockFileItem("buildMultiTargeting/_._")],
                ToolsAssemblies = [new LockFileItem("tools/_._")]
            };
            var library = new LockFileLibrary { Files = ["analyzers/dotnet/cs/_._"] };
            Assert.False(new ProjectAssetsFileService(new MockFileSystem(), null).SetIsDevDependency(null, target, library));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GetDotnetDependencys_UnresolvedEdgesRetainOnlyReachedTargetProvenance(bool reverse, bool isTestProject)
        {
            var runtimeFramework = NuGet.Frameworks.NuGetFramework.ParseFolder("net8.0");
            var devFramework = NuGet.Frameworks.NuGetFramework.ParseFolder("net10.0");
            LockFileTarget Target(NuGet.Frameworks.NuGetFramework framework, string child) => new()
            {
                TargetFramework = framework,
                Libraries = [new LockFileTargetLibrary
                {
                    Name = "Source", Version = NuGetVersion.Parse("1.0.0"),
                    RuntimeAssemblies = [new LockFileItem("lib/Source.dll")],
                    Dependencies =
                    [
                        new PackageDependency(child, VersionRange.Parse("[2.0.0, 3.0.0)")),
                        new PackageDependency("ResolvedChild", VersionRange.Parse("[1.0.0, 2.0.0)"))
                    ]
                },
                new LockFileTargetLibrary { Name = "ResolvedChild", Version = NuGetVersion.Parse("1.0.0") }]
            };
            var targets = new[] { Target(runtimeFramework, "MissingRuntimeChild"), Target(devFramework, "MissingDevChild") };
            var assets = new LockFile
            {
                Targets = (reverse ? targets.AsEnumerable().Reverse() : targets).ToList(),
                PackageSpec = new PackageSpec
                {
                    TargetFrameworks =
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = runtimeFramework,
                            Dependencies = [new LibraryDependency { LibraryRange = new LibraryRange("Source", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct }]
                        },
                        new TargetFrameworkInformation
                        {
                            FrameworkName = devFramework,
                            Dependencies = [new LibraryDependency { LibraryRange = new LibraryRange("Source", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = LibraryIncludeFlags.Build }]
                        }
                    }
                }
            };
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/project/obj/project.assets.json"] = "" });
            var reader = new Mock<IAssetFileReader>();
            reader.Setup(r => r.Read(It.IsAny<Stream>(), It.IsAny<string>())).Returns(assets);
            var packages = new ProjectAssetsFileService(fileSystem, () => reader.Object)
                .GetDotnetDependencys("/project/project.csproj", "/project/obj/project.assets.json", isTestProject);
            var source = packages.Single(p => p.Name == "Source");
            var runtimeEdge = new KeyValuePair<string, string>("MissingRuntimeChild", "[2.0.0, 3.0.0)");
            Assert.Equal(runtimeEdge, Assert.Single(source.NonDevelopmentUnresolvedDependencyEdges));
            if (isTestProject)
                Assert.Empty(source.RuntimeUnresolvedDependencyEdges);
            else
                Assert.Equal(runtimeEdge, Assert.Single(source.RuntimeUnresolvedDependencyEdges));
            Assert.Equal(3, source.GetDependencyEdges().Count());
            var resolvedChild = packages.Single(p => p.Name == "ResolvedChild");
            Assert.True(resolvedChild.IsNonDevelopmentReachable);
            Assert.Equal(!isTestProject, resolvedChild.IsRuntimeReachable);
            Assert.Empty(resolvedChild.NonDevelopmentUnresolvedDependencyEdges);
        }

        [Theory]
        [InlineData(LibraryIncludeFlags.All, false)]
        [InlineData(LibraryIncludeFlags.Build, true)]
        [InlineData(LibraryIncludeFlags.None, true)]
        [InlineData(LibraryIncludeFlags.Runtime, false)]
        [InlineData(LibraryIncludeFlags.Compile, false)]
        public void SetIsDevDependency_UsesSelectedAssetsNotPropagation(LibraryIncludeFlags include, bool expected)
        {
            var reference = new LibraryDependency { LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package), IncludeType = include, SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent };
            var target = new LockFileTargetLibrary
            {
                Build = [new LockFileItem("build/Package.targets")],
                CompileTimeAssemblies = [new LockFileItem("lib/net10.0/Package.dll")],
                RuntimeAssemblies = [new LockFileItem("lib/net10.0/Package.dll")]
            };
            Assert.Equal(expected, new ProjectAssetsFileService(new MockFileSystem(), null).SetIsDevDependency(reference, target, null));
        }

        [Fact]
        public void SetIsDevDependency_DefaultPrivateAssetsBuildOnly_IsDevDependency()
        {
            var target = new LockFileTargetLibrary { Build = [new LockFileItem("build/Package.targets")] };
            var reference = new LibraryDependency { LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package), SuppressParent = LibraryIncludeFlagUtils.DefaultSuppressParent };
            Assert.True(new ProjectAssetsFileService(new MockFileSystem(), null).SetIsDevDependency(reference, target, null));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void GetDotnetDependencys_ReachabilityIsResolvedBeforeFrameworkMerge(bool reverse, bool sharedRuntime, bool isTestProject)
        {
            var first = NuGet.Frameworks.NuGetFramework.ParseFolder("net8.0");
            var second = NuGet.Frameworks.NuGetFramework.ParseFolder("net10.0");
            LockFileTarget Target(NuGet.Frameworks.NuGetFramework framework, bool devDirect) => new()
            {
                TargetFramework = framework,
                Libraries =
                [
                    new LockFileTargetLibrary
                    {
                        Name = "RuntimeRoot", Version = NuGetVersion.Parse("1.0.0"),
                        Build = [new LockFileItem("build/RuntimeRoot.targets")],
                        RuntimeAssemblies = devDirect && sharedRuntime ? [new LockFileItem("lib/RuntimeRoot.dll")] : [],
                        Dependencies = [new PackageDependency("Shared", VersionRange.Parse("1.0.0"))]
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "BuildRoot", Version = NuGetVersion.Parse("1.0.0"),
                        Build = [new LockFileItem("build/BuildRoot.targets")],
                        Dependencies = [new PackageDependency(devDirect ? "Shared" : "Tool", VersionRange.Parse("1.0.0"))]
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Shared", Version = NuGetVersion.Parse("1.0.0"),
                        RuntimeAssemblies = [new LockFileItem("lib/Shared.dll")]
                    },
                    new LockFileTargetLibrary
                    {
                        Name = "Tool", Version = NuGetVersion.Parse("1.0.0"),
                        Build = [new LockFileItem("build/Tool.targets")],
                        Dependencies = [new PackageDependency("Shared", VersionRange.Parse("1.0.0"))]
                    }
                ]
            };
            var targets = new[] { Target(first, true), Target(second, false) };
            var assets = new LockFile
            {
                Targets = (reverse ? targets.AsEnumerable().Reverse() : targets).ToList(),
                PackageSpec = new PackageSpec
                {
                    TargetFrameworks =
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = first,
                            Dependencies =
                            [
                                new LibraryDependency { LibraryRange = new LibraryRange("RuntimeRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct },
                                new LibraryDependency { LibraryRange = new LibraryRange("BuildRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct },
                                new LibraryDependency { LibraryRange = new LibraryRange("Shared", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = LibraryIncludeFlags.Build }
                            ]
                        },
                        new TargetFrameworkInformation
                        {
                            FrameworkName = second,
                            Dependencies = [new LibraryDependency { LibraryRange = new LibraryRange("BuildRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct }]
                        }
                    }
                }
            };
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/project/obj/project.assets.json"] = "" });
            var reader = new Mock<IAssetFileReader>();
            reader.Setup(r => r.Read(It.IsAny<Stream>(), It.IsAny<string>())).Returns(assets);
            var packages = new ProjectAssetsFileService(fileSystem, () => reader.Object).GetDotnetDependencys("/project/project.csproj", "/project/obj/project.assets.json", isTestProject);
            var shared = packages.Single(p => p.Name == "Shared");
            Assert.True(shared.IsDirectReference);
            Assert.Equal(sharedRuntime, shared.IsNonDevelopmentReachable);
            Assert.Equal(sharedRuntime && !isTestProject, shared.IsRuntimeReachable);
            Assert.Equal(sharedRuntime && !isTestProject ? Component.ComponentScope.Required : Component.ComponentScope.Excluded, shared.Scope);
            var runtimeRoot = packages.Single(p => p.Name == "RuntimeRoot");
            Assert.Equal(sharedRuntime, runtimeRoot.IsNonDevelopmentReachable);
            Assert.Equal(sharedRuntime && !isTestProject, runtimeRoot.IsRuntimeReachable);
            Assert.False(packages.Single(p => p.Name == "BuildRoot").IsNonDevelopmentReachable);
            Assert.Equal(2, packages.Single(p => p.Name == "BuildRoot").GetDependencyEdges().Count());
        }

        [Fact]
        public void SetIsDevDependency_PrivateRuntimePackage_IsNotDevDependency()
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package),
                SuppressParent = LibraryIncludeFlags.All
            };
            var targetLibrary = new LockFileTargetLibrary
            {
                RuntimeAssemblies = [new LockFileItem("lib/net8.0/Package.dll")]
            };
            var library = new LockFileLibrary
            {
                Files = ["lib/net8.0/Package.dll"]
            };

            var service = new ProjectAssetsFileService(new MockFileSystem(), null);

            Assert.False(service.SetIsDevDependency(dependency, targetLibrary, library));
        }

        [Fact]
        public void SetIsDevDependency_PrivateBuildOnlyPackage_IsDevDependency()
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package),
                IncludeType = LibraryIncludeFlags.Analyzers,
                SuppressParent = LibraryIncludeFlags.All
            };
            var targetLibrary = new LockFileTargetLibrary();
            var library = new LockFileLibrary
            {
                Files = ["analyzers/dotnet/cs/Package.dll"]
            };

            var service = new ProjectAssetsFileService(new MockFileSystem(), null);

            Assert.True(service.SetIsDevDependency(dependency, targetLibrary, library));
        }

        [Fact]
        public void SetIsDevDependency_PrivatePackageWithNoSelectedAssets_IsNotDevDependency()
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange("Package", LibraryDependencyTarget.Package),
                SuppressParent = LibraryIncludeFlags.All
            };
            var targetLibrary = new LockFileTargetLibrary();
            var library = new LockFileLibrary();

            var service = new ProjectAssetsFileService(new MockFileSystem(), null);

            Assert.False(service.SetIsDevDependency(dependency, targetLibrary, library));
        }

        [Theory]
        [InlineData(".NETStandard", 2, 1, ".NETStandard,Version=v2.1")]
        [InlineData(".NETCoreApp", 6, 0, "net6.0")]
        public void GetDotnetDependencys_PackageAsTopLevelAndTransitive(string framework, int frameworkMajor, int frameworkMinor, string projectFileDependencyGroupsName)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReferences(
                        [
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
                        ])
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
                        Targets =
                        [
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ],
                                        Dependencies =
                                        [
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        ]
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package2",
                                        Version = new NuGet.Versioning.NuGetVersion("4.5.1"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package2.dll")
                                        ],
                                        Dependencies = []
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package3",
                                        Version = new NuGet.Versioning.NuGetVersion("1.0.0"),
                                        Build =
                                        [
                                            new LockFileItem("build/Package3.targets")
                                        ],
                                        Dependencies = []
                                    }
                                ]
                            },
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "win-x64",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ],
                                        Dependencies =
                                        [
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        ]
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package2",
                                        Version = new NuGet.Versioning.NuGetVersion("4.5.1"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package2.dll")
                                        ],
                                        Dependencies = []
                                    },
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package3",
                                        Version = new NuGet.Versioning.NuGetVersion("1.0.0"),
                                        Build =
                                        [
                                            new LockFileItem("build/Package3.targets")
                                        ],
                                        Dependencies = []
                                    }
                                ]
                            }
                        ],
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies =
                                    [
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
                                    ]
                                }
                            }
                        },
                        Libraries =
                        [
                            new LockFileLibrary
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        ],
                        ProjectFileDependencyGroups =
                        [

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" }),
                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package2 >= 4.5.1" }),
                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package3 >= 1.0.0" })
                        ]
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
                        [
                            new DotnetDependency
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                                Dependencies = new Dictionary<string, string>
                                {
                                    { "Package2", "[4.5, )" },
                                },
                            }
                        ])
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
                        Targets =
                        [
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ],
                                        Dependencies =
                                        [
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        ]
                                    }
                                ]
                            },
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "win-x64",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ],
                                        Dependencies =
                                        [
                                            new PackageDependency("Package2", new VersionRange(minVersion: new NuGetVersion("4.5.0"), originalString:"[4.5, )"))
                                        ]
                                    }
                                ]
                            }
                        ],
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies = [
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
                                    ]
                                }
                            }
                        },
                        Libraries =
                        [
                            new LockFileLibrary
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        ],
                        ProjectFileDependencyGroups =
                        [

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" })
                        ]
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
                        [
                            new DotnetDependency
                            {
                                Name = "Package1",
                                Version = "1.5.0",
                            }
                        ])
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
                        Targets =
                        [
                            new LockFileTarget
                            {                               
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ]
                                    }
                                ]
                            },
                            new LockFileTarget
                            {
                                TargetFramework = nuGetFramework,
                                RuntimeIdentifier = "win-x64",
                                Libraries =
                                [
                                    new LockFileTargetLibrary
                                    {
                                        Name = "Package1",
                                        Version = new NuGet.Versioning.NuGetVersion("1.5.0"),
                                        CompileTimeAssemblies =
                                        [
                                            new LockFileItem("Package1.dll")
                                        ]
                                    }
                                ]
                            }
                        ],
                        PackageSpec = new PackageSpec
                        {
                            TargetFrameworks =
                            {
                                new TargetFrameworkInformation
                                {
                                    FrameworkName = nuGetFramework,
                                    TargetAlias = nuGetFramework.Framework,
                                    Dependencies = [
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
                                    ]
                                    
                                }
                            }
                        },
                        Libraries =
                        [
                            new LockFileLibrary()
                            {
                                Name = "Package1",
                                Version = new NuGetVersion("1.5.0"),
                                Type = "package"
                            }
                        ],
                        ProjectFileDependencyGroups =
                        [

                            new ProjectFileDependencyGroup(projectFileDependencyGroupsName, new List<string> { "Package1 >= 1.5.0" })
                        ]
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
