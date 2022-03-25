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
using CycloneDX.Models.v1_3;
using System.Linq;
using NuGet.Versioning;

namespace CycloneDX.Services
{
    public class ProjectAssetsFileService : IProjectAssetsFileService 
    {
        private readonly IFileSystem _fileSystem;
        private readonly IDotnetCommandService _dotnetCommandService;
        private readonly Func<IAssetFileReader> _assetFileReaderFactory;

        public ProjectAssetsFileService(IFileSystem fileSystem, IDotnetCommandService dotnetCommandService, Func<IAssetFileReader> assetFileReaderFactory)
        {
            _fileSystem = fileSystem;
            _dotnetCommandService = dotnetCommandService;
            _assetFileReaderFactory = assetFileReaderFactory;
        }

        public HashSet<NugetPackage> GetNugetPackages(string projectFilePath, string projectAssetsFilePath, bool isTestProject)
        {
            var packages = new HashSet<NugetPackage>();

            if (_fileSystem.File.Exists(projectAssetsFilePath))
            {
                var assetFileReader = _assetFileReaderFactory();
                var assetsFile = assetFileReader.Read(projectAssetsFilePath);

                foreach (var targetRuntime in assetsFile.Targets)
                {
                    var directPackageDependencies = GetDirectPackageDependencies(targetRuntime.Name, projectFilePath);
                    var runtimePackages = new HashSet<NugetPackage>();
                    foreach (var library in targetRuntime.Libraries.Where(lib => lib.Type != "project"))
                    {
                        var package = new NugetPackage
                        {
                            Name = library.Name,
                            Version = library.Version.ToNormalizedString(),
                            Scope = Component.ComponentScope.Required,
                            Dependencies = new Dictionary<string, string>(),
                        };
                        var topLevelReferenceKey = (package.Name, package.Version);
                        if (directPackageDependencies.Contains(topLevelReferenceKey))
                        {
                            package.IsDirectReference = true;
                        }
                        // is this a test project dependency or only a development dependency
                        if (
                            isTestProject
                            || (
                                library.CompileTimeAssemblies.Count == 0
                                && library.ContentFiles.Count == 0
                                && library.EmbedAssemblies.Count == 0
                                && library.FrameworkAssemblies.Count == 0
                                && library.NativeLibraries.Count == 0
                                && library.ResourceAssemblies.Count == 0
                                && library.ToolsAssemblies.Count == 0
                            )
                        )
                        {
                            package.Scope = Component.ComponentScope.Excluded;
                        }
                        // include direct dependencies
                        foreach (var dep in library.Dependencies)
                        {
                            package.Dependencies.Add(dep.Id, dep.VersionRange?.ToNormalizedString());
                        }
                        runtimePackages.Add(package);
                    }

                    ResolveDependecyVersionRanges(runtimePackages);

                    packages.UnionWith(runtimePackages);
                }
            }

            return packages;
        }

        // Future: Instead of invoking the dotnet CLI to get direct dependencies, once asset file version 3 is available through the Nuget library,
        //         The direct dependencies could be retrieved from the asset file json path: .project.frameworks.<framework>.dependencies
        private List<(string, string)> GetDirectPackageDependencies(string targetRuntime, string projectFilePath)
        {
            var directPackageDependencies = new List<(string, string)>();
            var framework = TargetFrameworkToAlias(targetRuntime);
            if (framework != null)
            {
                var output = _dotnetCommandService.Run($"list \"{projectFilePath}\" package --framework {framework}");
                var result = output.Success ? output.StdOut : null;
                if (result != null)
                {
                    directPackageDependencies = result.Split('\r', '\n').Select(line => line.Trim())
                        .Where(line => line.StartsWith(">", StringComparison.InvariantCulture))
                        .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Where(parts => parts.Length == 4)
                        .Select(parts => (parts[1], parts[3]))
                        .ToList();
                }
            }
            return directPackageDependencies;
        }

        /// <summary>
        /// Converts an asset file's target framework value into a csproj target framework value.
        ///
        /// Examples:
        ///     .NetStandard,Version=V3.1 => netstandard3.1
        ///     .NetCoreApp,Version=V3.1  => netcoreapp3.1
        /// </summary>
        private string TargetFrameworkToAlias(string target)
        {
            target = target.ToLowerInvariant().TrimStart('.');
            var targetParts = target.Split(",version=v");
            if (targetParts.Length == 2)
            {
                return string.Join("", targetParts);
            }
            return null;
        }

        /// <summary>
        /// Updates all dependencies with version ranges to the version it was resolved to.
        /// </summary>
        private static void ResolveDependecyVersionRanges(HashSet<NugetPackage> runtimePackages)
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
                            Console.Error.WriteLine($"Dependency ({dependency.Value}) with version range ({dependency.Value}) did not resolve to a specific version.");
                        }
                    }
                }
            }
        }
    }
}
