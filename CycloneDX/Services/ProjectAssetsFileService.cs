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
using System.IO;
using System.Text.Json;

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

        public HashSet<NugetPackage> GetNugetPackages(string projectFilePath, string projectAssetsFilePath, bool isTestProject, bool excludeDev)
        {
            var packages = new HashSet<NugetPackage>();

            if (_fileSystem.File.Exists(projectAssetsFilePath))
            {
                var jsonContent = File.ReadAllText(projectAssetsFilePath);
                var assetFileObject = JsonDocument.Parse(jsonContent);
                // get all direct nuget dependencies of the project
                var frameworksProperties = assetFileObject.RootElement.GetProperty("project").GetProperty("frameworks");

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
                            // get value from project.assets.json file ( x."project"."frameworks".<framework>."dependencies".<library.Name>."suppressParent") 
                            IsDevDependency = SetIsDevDependency(library.Name, targetRuntime.Name, frameworksProperties)
                        };
                        var topLevelReferenceKey = (package.Name, package.Version);
                        if (directPackageDependencies.Contains(topLevelReferenceKey))
                        {
                            package.IsDirectReference = true;
                        }
                        // is this a test project dependency or only a development dependency
                        if (
                            isTestProject
                            || (package.IsDevDependency && excludeDev)
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

        public bool SetIsDevDependency(string packageName, string targetRuntime, JsonElement jsonContent)
        {
            string framework = TargetFrameworkToAlias(targetRuntime);
            JsonElement packageProperties;
            var output = System.Text.Json.JsonSerializer.Serialize(jsonContent);
            if (jsonContent.GetProperty(framework).GetProperty("dependencies").TryGetProperty(packageName, out packageProperties))
            {
                output = System.Text.Json.JsonSerializer.Serialize(packageProperties);
                // suppressParent: exists only for development dependencies
                return packageProperties.TryGetProperty("suppressParent", out _);
            }
            return false;
        }

        /// <summary>
        /// Converts an asset file's target framework value into a csproj target framework value.
        ///
        /// Examples:
        ///     .NetStandard,Version=V3.1 => netstandard3.1
        ///     .NetCoreApp,Version=V3.1  => netcoreapp3.1
        ///     netcoreapp3.1  => netcoreapp3.1
        ///     net6.0  => net6.0
        /// </summary>
        private string TargetFrameworkToAlias(string target)
        {
            if (!string.IsNullOrEmpty(target))
            {
                target = target.ToLowerInvariant().TrimStart('.');
                var targetParts = target.Split(",version=v");
                if (targetParts.Length == 2)
                {
                    return string.Join("", targetParts);
                }
                return targetParts[0];
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
                            Console.Error.WriteLine($"Dependency ({dependency.Key}) with version range ({dependency.Value}) did not resolve to a specific version.");
                        }
                    }
                }
            }
        }
    }
}
