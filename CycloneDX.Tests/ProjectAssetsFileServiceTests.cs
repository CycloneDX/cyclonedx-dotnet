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
using System.Linq;
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
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GetDotnetDependencys_Issue1139_RuntimeMetapackageRetainsClosure(bool referenceManagedDirectly, bool isTestProject)
        {
            // Reduced assets-file fixture matching the ONNX GPU 1.25.1 packaging shape:
            // buildTransitive-only parent, native GPU children, managed runtime, and Tensors.
            // Conservative promotion retains this runtime closure, including ambiguous
            // build-tool graphs with the same observable assets (not a publish inventory).
            var managedDependency = referenceManagedDirectly
                ? ", \"Microsoft.ML.OnnxRuntime.Managed\": { \"target\": \"Package\", \"version\": \"[1.25.1, )\" }"
                : "";
            var managedGroup = referenceManagedDirectly ? ", \"Microsoft.ML.OnnxRuntime.Managed >= 1.25.1\"" : "";
            var assetsJson = $$"""
                {
                  "version": 3,
                  "targets": {
                    "net10.0": {
                      "Microsoft.ML.OnnxRuntime.Gpu/1.25.1": {
                        "type": "package",
                        "dependencies": {
                          "Microsoft.ML.OnnxRuntime.Gpu.Linux": "1.25.1",
                          "Microsoft.ML.OnnxRuntime.Gpu.Windows": "1.25.1",
                          "Microsoft.ML.OnnxRuntime.Managed": "1.25.1"
                        },
                        "build": {
                          "buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.props": {},
                          "buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.targets": {}
                        }
                      },
                      "Microsoft.ML.OnnxRuntime.Gpu.Linux/1.25.1": {
                        "type": "package",
                        "dependencies": { "Microsoft.ML.OnnxRuntime.Managed": "1.25.1" },
                        "runtimeTargets": {
                          "runtimes/linux-x64/native/libonnxruntime.so": { "assetType": "native", "rid": "linux-x64" }
                        }
                      },
                      "Microsoft.ML.OnnxRuntime.Gpu.Windows/1.25.1": {
                        "type": "package",
                        "dependencies": { "Microsoft.ML.OnnxRuntime.Managed": "1.25.1" },
                        "runtimeTargets": {
                          "runtimes/win-x64/native/onnxruntime.dll": { "assetType": "native", "rid": "win-x64" }
                        }
                      },
                      "Microsoft.ML.OnnxRuntime.Managed/1.25.1": {
                        "type": "package",
                        "dependencies": { "System.Numerics.Tensors": "9.0.0" },
                        "compile": { "lib/net8.0/Microsoft.ML.OnnxRuntime.dll": {} },
                        "runtime": { "lib/net8.0/Microsoft.ML.OnnxRuntime.dll": {} }
                      },
                      "System.Numerics.Tensors/9.0.0": {
                        "type": "package",
                        "runtime": { "lib/net9.0/System.Numerics.Tensors.dll": {} }
                      }
                    }
                  },
                  "libraries": {
                    "Microsoft.ML.OnnxRuntime.Gpu/1.25.1": { "type": "package", "files": [] },
                    "Microsoft.ML.OnnxRuntime.Gpu.Linux/1.25.1": { "type": "package", "files": [] },
                    "Microsoft.ML.OnnxRuntime.Gpu.Windows/1.25.1": { "type": "package", "files": [] },
                    "Microsoft.ML.OnnxRuntime.Managed/1.25.1": { "type": "package", "files": [] },
                    "System.Numerics.Tensors/9.0.0": { "type": "package", "files": [] }
                  },
                  "projectFileDependencyGroups": {
                    "net10.0": ["Microsoft.ML.OnnxRuntime.Gpu >= 1.25.1"{{managedGroup}}]
                  },
                  "project": {
                    "version": "1.0.0",
                    "frameworks": {
                      "net10.0": {
                        "targetAlias": "net10.0",
                        "dependencies": {
                          "Microsoft.ML.OnnxRuntime.Gpu": { "target": "Package", "version": "[1.25.1, )" }{{managedDependency}}
                        }
                      }
                    }
                  }
                }
                """;
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/project/obj/project.assets.json"] = assetsJson });
            var packages = new ProjectAssetsFileService(fileSystem, () => new AssetFileReader())
                .GetDotnetDependencys("/project/project.csproj", "/project/obj/project.assets.json", isTestProject);

            Assert.Equal(5, packages.Count);
            var parent = packages.Single(p => p.Name == "Microsoft.ML.OnnxRuntime.Gpu");
            Assert.True(parent.IsDirectReference);
            Assert.False(parent.IsDevDependency);
            Assert.Equal(3, parent.Dependencies.Count);
            Assert.All(packages.Where(p => p != parent), p => Assert.False(p.IsDevDependency));
            Assert.All(packages, p =>
            {
                Assert.True(p.IsNonDevelopmentReachable);
                Assert.Equal(!isTestProject, p.IsRuntimeReachable);
                Assert.Equal(isTestProject ? Component.ComponentScope.Excluded : Component.ComponentScope.Required, p.Scope);
            });
        }

        private static HashSet<DotnetDependency> ReadAssets(LockFile assets, bool isTestProject = false)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/project/obj/project.assets.json"] = "" });
            var reader = new Mock<IAssetFileReader>();
            reader.Setup(r => r.Read(It.IsAny<Stream>(), It.IsAny<string>())).Returns(assets);
            return new ProjectAssetsFileService(fileSystem, () => reader.Object)
                .GetDotnetDependencys("/project/project.csproj", "/project/obj/project.assets.json", isTestProject);
        }

        private static LockFileTargetLibrary GraphPackage(string name, params string[] children) => new()
        {
            Name = name, Version = NuGetVersion.Parse("1.0.0"), Type = "package",
            Dependencies = children.Select(child => new PackageDependency(child, VersionRange.Parse("[1.0.0, 2.0.0)"))).ToList()
        };

        private static LibraryDependency Direct(string name, LibraryIncludeFlags include = LibraryIncludeFlags.All) => new()
        {
            LibraryRange = new LibraryRange(name, LibraryDependencyTarget.Package),
            ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = include,
            SuppressParent = LibraryIncludeFlags.All // Propagation is deliberately irrelevant.
        };

        private static LockFile GraphAssets(IEnumerable<LockFileTargetLibrary> libraries, params LibraryDependency[] references)
        {
            var framework = NuGet.Frameworks.NuGetFramework.ParseFolder("net10.0");
            return new LockFile
            {
                Targets = [new LockFileTarget { TargetFramework = framework, Libraries = libraries.ToList() }],
                PackageSpec = new PackageSpec
                {
                    TargetFrameworks = { new TargetFrameworkInformation { FrameworkName = framework, Dependencies = references.ToImmutableArray() } }
                }
            };
        }

        private static void AddEvidence(LockFileTargetLibrary package, string kind, string path = "asset.dll")
        {
            switch (kind)
            {
                case "runtime": package.RuntimeAssemblies.Add(new LockFileItem(path)); break;
                case "resource": package.ResourceAssemblies.Add(new LockFileItem(path)); break;
                case "runtimeTarget": package.RuntimeTargets.Add(new LockFileRuntimeTarget(path)); break;
                case "nativeTarget":
                    var native = new LockFileRuntimeTarget(path);
                    native.Properties["assetType"] = "native";
                    package.RuntimeTargets.Add(native);
                    break;
                case "native": package.NativeLibraries.Add(new LockFileItem(path)); break;
                case "compile": package.CompileTimeAssemblies.Add(new LockFileItem(path)); break;
                case "frameworkAssembly": package.FrameworkAssemblies.Add(path); break;
                case "frameworkReference": package.FrameworkReferences.Add(path); break;
                case "embed": package.EmbedAssemblies.Add(new LockFileItem(path)); break;
                case "content": package.ContentFiles.Add(new LockFileContentFile(path)); break;
                case "build": package.Build.Add(new LockFileItem(path)); break;
                case "multiBuild": package.BuildMultiTargeting.Add(new LockFileItem(path)); break;
                case "tools": package.ToolsAssemblies.Add(new LockFileItem(path)); break;
            }
        }

        [Theory]
        [InlineData(LibraryIncludeFlags.Runtime, "runtime", true)]
        [InlineData(LibraryIncludeFlags.Runtime, "resource", true)]
        [InlineData(LibraryIncludeFlags.Runtime, "runtimeTarget", true)]
        [InlineData(LibraryIncludeFlags.Native, "native", true)]
        [InlineData(LibraryIncludeFlags.Native, "nativeTarget", true)]
        [InlineData(LibraryIncludeFlags.Compile, "compile", true)]
        [InlineData(LibraryIncludeFlags.Compile, "frameworkAssembly", true)]
        [InlineData(LibraryIncludeFlags.Compile, "frameworkReference", true)]
        [InlineData(LibraryIncludeFlags.Compile, "embed", true)]
        [InlineData(LibraryIncludeFlags.ContentFiles, "content", true)]
        [InlineData(LibraryIncludeFlags.Native, "runtime", false)]
        [InlineData(LibraryIncludeFlags.Runtime, "nativeTarget", false)]
        [InlineData(LibraryIncludeFlags.Compile, "content", false)]
        [InlineData(LibraryIncludeFlags.ContentFiles, "compile", false)]
        [InlineData(LibraryIncludeFlags.Build, "runtime", false)]
        [InlineData(LibraryIncludeFlags.None, "runtime", false)]
        [InlineData(LibraryIncludeFlags.Analyzers, "runtime", false)]
        public void GetDotnetDependencys_PromotionIntersectsRootCategoriesWithRestoredInventory(
            LibraryIncludeFlags allowed, string kind, bool promoted)
        {
            var root = GraphPackage("Root", "cHiLd");
            AddEvidence(root, "build", "buildTransitive/Root.targets");
            var child = GraphPackage("Child");
            AddEvidence(child, kind);
            // Child's explicit direct selection is NOT a restriction on Root -> Child.
            var selection = allowed == LibraryIncludeFlags.None ? allowed : allowed | LibraryIncludeFlags.BuildTransitive;
            var packages = ReadAssets(GraphAssets([root, child], Direct("Root", selection), Direct("Child", LibraryIncludeFlags.Build)));
            var wrapper = packages.Single(p => p.Name == "Root");
            var restoredChild = packages.Single(p => p.Name == "Child");
            Assert.Equal(!promoted, wrapper.IsDevDependency);
            Assert.Equal(promoted, wrapper.IsRuntimeReachable);
            Assert.True(restoredChild.IsDevDependency);
            Assert.Equal(promoted, restoredChild.IsNonDevelopmentReachable);
            Assert.Equal(promoted ? Component.ComponentScope.Required : Component.ComponentScope.Excluded, restoredChild.Scope);
            Assert.Equal("1.0.0", wrapper.Dependencies["cHiLd"]);
        }

        [Theory]
        [InlineData("runtime", "lib/_._")]
        [InlineData("runtime", "")]
        [InlineData("nativeTarget", "runtimes/native/_._")]
        [InlineData("compile", "ref\\_._")]
        [InlineData("content", "content/_._")]
        [InlineData("unknown", "asset.dll")]
        public void GetDotnetDependencys_PlaceholderAndAssetlessChildrenCannotPromote(string kind, string path)
        {
            var root = GraphPackage("Root", "Child");
            AddEvidence(root, "build");
            var child = GraphPackage("Child");
            AddEvidence(child, kind, path);
            var packages = ReadAssets(GraphAssets([root, child], Direct("Root")));
            Assert.True(packages.Single(p => p.Name == "Root").IsDevDependency);
            Assert.False(packages.Single(p => p.Name == "Child").IsDevDependency);
            Assert.All(packages, p => Assert.False(p.IsRuntimeReachable));
        }

        [Theory]
        [InlineData("analyzer", false)]
        [InlineData("tools", false)]
        [InlineData("buildAnalyzer", false)]
        [InlineData("buildTools", false)]
        [InlineData("analyzer", true)]
        [InlineData("tools", true)]
        [InlineData("buildAnalyzer", true)]
        [InlineData("buildTools", true)]
        public void GetDotnetDependencys_AnalyzerToolsBoundariesRequireOwnInventory(string kind, bool ownRuntime)
        {
            var root = GraphPackage("Root", "Boundary");
            AddEvidence(root, "build");
            var boundary = GraphPackage("Boundary", "Child");
            if (kind.Contains("build", StringComparison.Ordinal)) AddEvidence(boundary, "build");
            if (kind.Contains("ools", StringComparison.Ordinal)) AddEvidence(boundary, "tools");
            if (ownRuntime) AddEvidence(boundary, "runtime");
            var child = GraphPackage("Child");
            AddEvidence(child, "runtime");
            var assets = GraphAssets([root, boundary, child], Direct("Root"));
            assets.Libraries.Add(new LockFileLibrary
            {
                Name = "Boundary", Version = boundary.Version,
                Files = kind.Contains("nalyzer", StringComparison.Ordinal) ? ["analyzers/dotnet/Boundary.dll"] : []
            });
            var packages = ReadAssets(assets);
            Assert.Equal(!ownRuntime, packages.Single(p => p.Name == "Root").IsDevDependency);
            Assert.Equal(!ownRuntime, packages.Single(p => p.Name == "Boundary").IsDevDependency);
            Assert.All(packages, p => Assert.Equal(ownRuntime, p.IsRuntimeReachable));
        }

        [Theory]
        [InlineData("analyzer")]
        [InlineData("tools")]
        public void GetDotnetDependencys_MixedBuildDevelopmentRootDoesNotPromoteOrBlockNormalReachability(string kind)
        {
            var root = GraphPackage("Root", "Child");
            AddEvidence(root, "build");
            if (kind == "tools") AddEvidence(root, "tools");
            var child = GraphPackage("Child");
            AddEvidence(child, "runtime");
            var runtime = GraphPackage("Runtime", "Root");
            AddEvidence(runtime, "runtime");
            var assets = GraphAssets([root, child, runtime], Direct("Root"), Direct("Runtime"));
            assets.Libraries.Add(new LockFileLibrary { Name = "Root", Version = root.Version,
                Files = kind == "analyzer" ? ["analyzers/dotnet/Root.dll"] : [] });
            var packages = ReadAssets(assets);
            Assert.True(packages.Single(p => p.Name == "Root").IsDevDependency);
            Assert.All(packages, p => Assert.True(p.IsRuntimeReachable));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GetDotnetDependencys_NestedCyclicWrappersNeedActualEvidence(bool reverse, bool positive)
        {
            var root = GraphPackage("Root", "Nested");
            AddEvidence(root, "multiBuild");
            var nested = GraphPackage("Nested", "Root", "Child");
            AddEvidence(nested, "build");
            var child = GraphPackage("Child");
            if (positive) AddEvidence(child, "runtime");
            var libraries = new[] { root, nested, child };
            var packages = ReadAssets(GraphAssets(reverse ? libraries.AsEnumerable().Reverse() : libraries, Direct("Root")));
            Assert.All(packages.Where(p => p.Name != "Child"), p => Assert.Equal(!positive, p.IsDevDependency));
            Assert.All(packages, p => Assert.Equal(positive, p.IsRuntimeReachable));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetDotnetDependencys_MissingOrIncompatibleEdgesCannotPromote(bool missing)
        {
            var root = GraphPackage("Root", missing ? "Missing" : "Child");
            AddEvidence(root, "build");
            var child = GraphPackage("Child");
            child.Version = NuGetVersion.Parse("2.0.0");
            AddEvidence(child, "runtime");
            var packages = ReadAssets(GraphAssets([root, child], Direct("Root")));
            var wrapper = packages.Single(p => p.Name == "Root");
            Assert.True(wrapper.IsDevDependency);
            Assert.All(packages, p => Assert.False(p.IsRuntimeReachable));
            Assert.Empty(wrapper.RuntimeUnresolvedDependencyEdges);
        }

        [Fact]
        public void GetDotnetDependencys_DefaultBuildClosuresPromoteDeliberatelyUnlikeExplicitBuildSelection()
        {
            // Policy regression separated from framework-merge assertions: unrestricted
            // build-only wrappers now retain runtime-bearing closures conservatively.
            var root = GraphPackage("BuildRoot", "Tool");
            AddEvidence(root, "build");
            var tool = GraphPackage("Tool", "Shared");
            AddEvidence(tool, "build");
            var shared = GraphPackage("Shared");
            AddEvidence(shared, "runtime");
            var standalone = GraphPackage("Standalone");
            AddEvidence(standalone, "build");
            var placeholderRoot = GraphPackage("PlaceholderRoot", "Shared");
            AddEvidence(placeholderRoot, "build", "build/_._");
            var assets = GraphAssets([root, tool, shared, standalone, placeholderRoot], Direct("BuildRoot"), Direct("Standalone"),
                Direct("PlaceholderRoot", LibraryIncludeFlags.Build));
            var packages = ReadAssets(assets);
            Assert.All(packages.Where(p => p.Name is "BuildRoot" or "Tool" or "Shared"), p =>
            {
                Assert.False(p.IsDevDependency);
                Assert.True(p.IsRuntimeReachable);
            });
            Assert.All(packages.Where(p => p.Name is "Standalone" or "PlaceholderRoot"), p =>
            {
                Assert.True(p.IsDevDependency);
                Assert.False(p.IsRuntimeReachable);
            });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void GetDotnetDependencys_PromotionEvidenceDoesNotCrossRidTargets(bool reverse, bool isTestProject)
        {
            var positiveRoot = GraphPackage("Root", "LocalChild", "MissingRuntime");
            AddEvidence(positiveRoot, "build");
            var positiveChild = GraphPackage("LocalChild");
            AddEvidence(positiveChild, "native");
            var negativeRoot = GraphPackage("Root", "LocalChild", "DevOnlyChild");
            AddEvidence(negativeRoot, "build");
            var negativeChild = GraphPackage("LocalChild"); // Same name/version, no assets in this RID.
            var devOnlyChild = GraphPackage("DevOnlyChild");
            var assets = GraphAssets([positiveRoot, positiveChild], Direct("Root"));
            var positive = assets.Targets.Single();
            positive.RuntimeIdentifier = "linux-x64";
            var negative = new LockFileTarget
            {
                TargetFramework = positive.TargetFramework, RuntimeIdentifier = "win-x64",
                Libraries = [negativeRoot, negativeChild, devOnlyChild]
            };
            assets.Targets = reverse ? [negative, positive] : [positive, negative];
            if (reverse)
            {
                positive.Libraries = positive.Libraries.Reverse().ToList();
                negative.Libraries = negative.Libraries.Reverse().ToList();
            }
            var packages = ReadAssets(assets, isTestProject);
            var root = packages.Single(p => p.Name == "Root");
            Assert.False(root.IsDevDependency);
            Assert.True(root.IsNonDevelopmentReachable);
            Assert.Equal(!isTestProject, root.IsRuntimeReachable);
            Assert.Equal(new KeyValuePair<string, string>("MissingRuntime", "[1.0.0, 2.0.0)"),
                Assert.Single(root.NonDevelopmentUnresolvedDependencyEdges));
            Assert.Equal(isTestProject ? 0 : 1, root.RuntimeUnresolvedDependencyEdges.Count);
            var devOnly = packages.Single(p => p.Name == "DevOnlyChild");
            Assert.False(devOnly.IsNonDevelopmentReachable);
            Assert.Equal(Component.ComponentScope.Excluded, devOnly.Scope);
            Assert.Equal(3, root.GetDependencyEdges().Count());
        }

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
            // Merge/isolation regression, not default build-wrapper policy (covered
            // separately): development roots explicitly select only Build assets.
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
                                 new LibraryDependency { LibraryRange = new LibraryRange("RuntimeRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = sharedRuntime ? LibraryIncludeFlags.All : LibraryIncludeFlags.Build },
                                 new LibraryDependency { LibraryRange = new LibraryRange("BuildRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = LibraryIncludeFlags.Build },
                                new LibraryDependency { LibraryRange = new LibraryRange("Shared", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = LibraryIncludeFlags.Build }
                            ]
                        },
                        new TargetFrameworkInformation
                        {
                            FrameworkName = second,
                             Dependencies = [new LibraryDependency { LibraryRange = new LibraryRange("BuildRoot", LibraryDependencyTarget.Package), ReferenceType = LibraryDependencyReferenceType.Direct, IncludeType = LibraryIncludeFlags.Build }]
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
