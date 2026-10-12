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
using System.IO.Abstractions;
using CycloneDX.Models;
using System.Linq;
using CycloneDX.Interfaces;
using NuGet.Versioning;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using System.IO;

namespace CycloneDX.Services
{
    public class ProjectAssetsFileService : IProjectAssetsFileService 
    {
        private readonly IFileSystem _fileSystem;
        private readonly Func<IAssetFileReader> _assetFileReaderFactory;

        public ProjectAssetsFileService(IFileSystem fileSystem, Func<IAssetFileReader> assetFileReaderFactory)
        {
            _fileSystem = fileSystem;
            _assetFileReaderFactory = assetFileReaderFactory;
        }

        public HashSet<DotnetDependency> GetDotnetDependencys(string projectFilePath, string projectAssetsFilePath, bool isTestProject)
        {
            var packages = new HashSet<DotnetDependency>();

            if (_fileSystem.File.Exists(projectAssetsFilePath))
            {
                var assetFileReader = _assetFileReaderFactory();
                using var fileStream = _fileSystem.FileStream.New(projectAssetsFilePath, FileMode.Open, FileAccess.Read);
                var assetsFile = assetFileReader.Read(fileStream, projectAssetsFilePath);
                

                foreach (var targetRuntime in assetsFile.Targets)
                {
                    var runtimePackages = new HashSet<DotnetDependency>();
                    var assetContexts = new Dictionary<DotnetDependency, (LibraryIncludeFlags Allowed, AssetEvidence Selected, AssetEvidence Inventory)>();
                    var targetFramework = assetsFile.PackageSpec.GetTargetFramework(targetRuntime.TargetFramework);
                    var dependencies = targetFramework.Dependencies;
                    var directDependencies = assetsFile.ProjectFileDependencyGroups
                        .Where(f => f.FrameworkName == targetRuntime.TargetFramework.GetShortFolderName() ||
                            f.FrameworkName == targetRuntime.TargetFramework.ToString()).SelectMany(p => p.Dependencies)
                        .Select(d =>
                        {
                            var x = d.Split(" ");
                            return new { Name = x.First() };

                        });

                    foreach (var lockFileLibrary in targetRuntime.Libraries)
                    {
                        var libs = dependencies.FirstOrDefault(ld => ld.Name.Equals(lockFileLibrary.Name, StringComparison.OrdinalIgnoreCase));
                        //try to find dependency in the library section, to get its path
                        var library = assetsFile.Libraries.FirstOrDefault(lib => string.Equals(lib.Name, lockFileLibrary.Name, StringComparison.OrdinalIgnoreCase) && lib.Version == lockFileLibrary.Version);

                        var package = new DotnetDependency
                        {
                            Name = lockFileLibrary.Name,
                            Version = lockFileLibrary.Version.ToNormalizedString(),
                            Scope = Component.ComponentScope.Required,
                            Dependencies = new Dictionary<string, string>(),
                            IsDevDependency = SetIsDevDependency(libs, lockFileLibrary, library),
                            IsDirectReference = SetIsDirectReference(libs) || directDependencies.Any(d => string.Equals(d.Name, lockFileLibrary.Name, StringComparison.OrdinalIgnoreCase)),
                            DependencyType = (lockFileLibrary.Type != "project") ? DependencyType.Package : DependencyType.Project,
                            Path = Path.Combine(Path.GetDirectoryName(projectFilePath), library?.Path ?? "")
                        };

                        // Test-project packages are always excluded (whole-project decision).
                        // Dev dependency scope is resolved later in Runner via BFS so that
                        // transitive packages reachable through both a dev dep and a runtime
                        // dep are correctly kept as scope=Required.
                        if (isTestProject)
                        {
                            package.Scope = Component.ComponentScope.Excluded;
                        }
                        // include direct dependencies
                        foreach (var dep in lockFileLibrary.Dependencies)
                        {
                            package.Dependencies.Add(dep.Id, dep.VersionRange?.ToNormalizedString());
                        }
                        runtimePackages.Add(package);
                        var selected = libs?.IncludeType ?? LibraryIncludeFlags.All;
                        assetContexts.Add(package, (selected, AnalyzeAssets(lockFileLibrary, library, selected),
                            AnalyzeAssets(lockFileLibrary, library, LibraryIncludeFlags.All)));
                    }

                    var allDependencies = runtimePackages.SelectMany(y => y.Dependencies.Keys).Distinct();
                    var allPackages = runtimePackages.Select(p => p.Name);
                    var packagesNotInAllPackages = allDependencies.Except(allPackages);

                    // Check if there is an "unresolved" dependency on NetStandard                    
                    if (packagesNotInAllPackages.Any(p => p == "NETStandard.Library"))
                    {
                        // If a project library has targets .net standard it actually doesn't resolve this dependency
                        // instead it is expected to find the Standard-Libraries on the target system
                        // => the libraries not being part of the resulting application and thus should not be included in
                        // the sbom anyways
                        foreach (var item in runtimePackages)
                        {
                            item.Dependencies.Remove("NETStandard.Library");
                        }
                    }

                    ResolveDependencyVersionRanges(runtimePackages);
                    PromoteRuntimeMetapackages(runtimePackages, assetContexts);
                    SetRuntimeReachability(runtimePackages, isTestProject);
                    DotnetDependency.MergeInto(packages, runtimePackages);
                }
            }

            return packages;
        }
        public bool SetIsDirectReference(LibraryDependency dependency)
        {
            return dependency?.ReferenceType == LibraryDependencyReferenceType.Direct;
        }
        public bool SetIsDevDependency(
            LibraryDependency dependency,
            LockFileTargetLibrary targetLibrary,
            LockFileLibrary library)
        {
            if (targetLibrary == null)
            {
                return false;
            }

            var selected = dependency?.IncludeType ?? LibraryIncludeFlags.All;
            if (selected == LibraryIncludeFlags.None)
                return true;

            var evidence = AnalyzeAssets(targetLibrary, library, selected);
            return evidence.Categories == LibraryIncludeFlags.None &&
                (evidence.Build || evidence.Analyzers || evidence.Tools || (selected & RuntimeCategories) == 0);
        }

        private const LibraryIncludeFlags RuntimeCategories = LibraryIncludeFlags.Compile | LibraryIncludeFlags.Runtime |
            LibraryIncludeFlags.Native | LibraryIncludeFlags.ContentFiles;

        private sealed record AssetEvidence(LibraryIncludeFlags Categories, bool Build, bool Analyzers, bool Tools);

        private static AssetEvidence AnalyzeAssets(LockFileTargetLibrary targetLibrary, LockFileLibrary library, LibraryIncludeFlags selected)
        {
            var analyzers =
                (selected & LibraryIncludeFlags.Analyzers) != 0 &&
                library?.Files.Any(path =>
                    IsAsset(path) && path.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase)) == true;
            var build =
                ((selected & (LibraryIncludeFlags.Build | LibraryIncludeFlags.BuildTransitive)) != 0 && targetLibrary.Build.Any(item => IsAsset(item.Path))) ||
                ((selected & LibraryIncludeFlags.Build) != 0 && targetLibrary.BuildMultiTargeting.Any(item => IsAsset(item.Path)));
            var categories = LibraryIncludeFlags.None;
            if ((selected & LibraryIncludeFlags.Runtime) != 0 &&
                (targetLibrary.RuntimeAssemblies.Any(item => IsAsset(item.Path)) || targetLibrary.ResourceAssemblies.Any(item => IsAsset(item.Path)) ||
                 targetLibrary.RuntimeTargets.Any(item => IsAsset(item.Path) && (!item.Properties.TryGetValue("assetType", out var assetType) || assetType != "native"))))
                categories |= LibraryIncludeFlags.Runtime;
            if ((selected & LibraryIncludeFlags.Compile) != 0 &&
                (targetLibrary.CompileTimeAssemblies.Any(item => IsAsset(item.Path)) || targetLibrary.FrameworkAssemblies.Any(IsAsset) ||
                 targetLibrary.FrameworkReferences.Any(IsAsset) || targetLibrary.EmbedAssemblies.Any(item => IsAsset(item.Path))))
                categories |= LibraryIncludeFlags.Compile;
            if ((selected & LibraryIncludeFlags.Native) != 0 &&
                (targetLibrary.NativeLibraries.Any(item => IsAsset(item.Path)) || targetLibrary.RuntimeTargets.Any(item =>
                    IsAsset(item.Path) && item.Properties.TryGetValue("assetType", out var assetType) && assetType == "native")))
                categories |= LibraryIncludeFlags.Native;
            if ((selected & LibraryIncludeFlags.ContentFiles) != 0 && targetLibrary.ContentFiles.Any(item => IsAsset(item.Path)))
                categories |= LibraryIncludeFlags.ContentFiles;
            return new AssetEvidence(categories, build, analyzers, targetLibrary.ToolsAssemblies.Any(item => IsAsset(item.Path)));
        }

        private static void PromoteRuntimeMetapackages(HashSet<DotnetDependency> packages,
            Dictionary<DotnetDependency, (LibraryIncludeFlags Allowed, AssetEvidence Selected, AssetEvidence Inventory)> contexts)
        {
            var lookup = packages.ToLookup(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var predecessors = packages.ToDictionary(p => p, _ => new List<DotnetDependency>());
            var evidence = packages.ToDictionary(p => p, p => contexts[p].Inventory.Categories);
            foreach (var source in packages)
            {
                var inventory = contexts[source].Inventory;
                // Inventory, not a different direct reference's IncludeType, governs this
                // dependency path. Pure analyzer/tool occurrences are evidence boundaries.
                if (inventory.Categories == LibraryIncludeFlags.None && (inventory.Analyzers || inventory.Tools))
                    continue;
                foreach (var edge in source.GetDependencyEdges())
                {
                    foreach (var child in lookup[edge.Key].Where(p => p.Version == edge.Value ||
                        (VersionRange.TryParse(edge.Value, out var range) &&
                         NuGetVersion.TryParse(p.Version, out var version) && range.Satisfies(version))))
                        predecessors[child].Add(source);
                }
            }

            // Only real assets seed the monotone worklist; empty cycles cannot justify
            // themselves. No historical single-name fallback or merged-target evidence.
            var queue = new Queue<DotnetDependency>(packages.Where(p => evidence[p] != LibraryIncludeFlags.None));
            while (queue.Count > 0)
            {
                var child = queue.Dequeue();
                foreach (var source in predecessors[child])
                {
                    var combined = evidence[source] | evidence[child];
                    if (combined == evidence[source])
                        continue;
                    evidence[source] = combined;
                    queue.Enqueue(source);
                }
            }
            foreach (var package in packages)
            {
                var context = contexts[package];
                if (package.IsDevDependency && context.Selected.Build &&
                    !context.Selected.Analyzers && !context.Selected.Tools &&
                    (context.Allowed & RuntimeCategories & evidence[package]) != 0)
                    package.IsDevDependency = false;
            }
        }

        private static bool IsAsset(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                !string.Equals(path.Replace('\\', '/').Split('/').Last(), "_._", StringComparison.Ordinal);
        }

        private static void SetRuntimeReachability(HashSet<DotnetDependency> packages, bool isTestProject)
        {
            var lookup = packages.ToLookup(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var reachable = new HashSet<DotnetDependency>();
            var queue = new Queue<DotnetDependency>();
            foreach (var package in packages.Where(p => p.IsDirectReference && !p.IsDevDependency))
            {
                reachable.Add(package);
                queue.Enqueue(package);
            }
            while (queue.Count > 0)
            {
                var source = queue.Dequeue();
                foreach (var edge in source.GetDependencyEdges())
                {
                    var candidates = lookup[edge.Key].ToList();
                    var matches = candidates.Where(p => p.Version == edge.Value).ToList();
                    if (matches.Count == 0 && VersionRange.TryParse(edge.Value, out var range))
                        matches = candidates.Where(p => NuGetVersion.TryParse(p.Version, out var version) && range.Satisfies(version)).ToList();
                    if (matches.Count == 0)
                    {
                        source.NonDevelopmentUnresolvedDependencyEdges.Add(edge);
                        if (!isTestProject)
                            source.RuntimeUnresolvedDependencyEdges.Add(edge);
                    }
                    // Keep the historical one-name scope fallback, but do not let it
                    // erase an unsatisfied range needed for post-merge resolution.
                    if (matches.Count == 0 && candidates.Count == 1)
                        matches = candidates;
                    foreach (var child in matches)
                    {
                        if (reachable.Add(child))
                            queue.Enqueue(child);
                    }
                }
            }
            foreach (var package in packages)
            {
                package.IsNonDevelopmentReachable = reachable.Contains(package);
                package.IsRuntimeReachable = !isTestProject && package.IsNonDevelopmentReachable.Value;
                package.Scope = package.IsRuntimeReachable.Value ? Component.ComponentScope.Required : Component.ComponentScope.Excluded;
            }
        }

        /// <summary>
        /// Updates all dependencies with version ranges to the version it was resolved to.
        /// </summary>
        private static void ResolveDependencyVersionRanges(HashSet<DotnetDependency> runtimePackages)
        {
            var runtimePackagesLookup = runtimePackages.ToLookup(x => x.Name.ToLowerInvariant());
            foreach (var runtimePackage in runtimePackages)
            {
                foreach (var dependency in runtimePackage.Dependencies.ToList())
                {
                    if (!NuGetVersion.TryParse(dependency.Value, out _) && VersionRange.TryParse(dependency.Value, out VersionRange versionRange))
                    {
                        var normalizedDependencyKey = dependency.Key.ToLowerInvariant();
                        var package = runtimePackagesLookup[normalizedDependencyKey].FirstOrDefault(pkg => versionRange.Satisfies(NuGetVersion.Parse(pkg.Version)));
                        if (package != default)
                        {
                            runtimePackage.Dependencies[dependency.Key] = package.Version;
                        }
                        else
                        {
                            // This should not happen, since all dependencies are resolved to a specific version.
                            Console.Error.WriteLine($"Dependency ({dependency.Key}) with version range ({dependency.Value}) referenced by (Name:{runtimePackage.Name} Version:{runtimePackage.Version}) did not resolve to a specific version.");
                        }
                    }
                }
            }
        }
    }
}
