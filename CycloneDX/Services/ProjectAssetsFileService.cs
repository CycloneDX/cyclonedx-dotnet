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
using System.Text.Json;
using NuGet.Frameworks;

namespace CycloneDX.Services
{
    public class ProjectAssetsFileService : IProjectAssetsFileService 
    {
        private readonly IFileSystem _fileSystem;
        private readonly IDotnetCommandService _dotnetCommandService;
        private readonly Func<IAssetFileReader> _assetFileReaderFactory;
        private readonly IJsonDocs _assetJsonObject;

        public ProjectAssetsFileService(IFileSystem fileSystem, IDotnetCommandService dotnetCommandService, Func<IAssetFileReader> assetFileReaderFactory, IJsonDocs assetJsonObject)
        {
            _fileSystem = fileSystem;
            _dotnetCommandService = dotnetCommandService;
            _assetFileReaderFactory = assetFileReaderFactory;
            _assetJsonObject = assetJsonObject;
        }

        public HashSet<NugetPackage> GetNugetPackages(string projectFilePath, string projectAssetsFilePath, bool isTestProject, bool excludeDev)
        {
            var packages = new HashSet<NugetPackage>();

            if (_fileSystem.File.Exists(projectAssetsFilePath))
            {
                var assetFileReader = _assetFileReaderFactory();
                string jsonContent = assetFileReader.ReadAllText(projectAssetsFilePath);
                JsonDocument assetFileObject = _assetJsonObject.Parse(jsonContent);
                // get all direct nuget dependencies of the project
                JsonElement frameworksProperties = assetFileObject.RootElement.GetProperty("project").GetProperty("frameworks");

                var assetsFile = assetFileReader.Read(projectAssetsFilePath);

                foreach (var targetRuntime in assetsFile.Targets)
                {
                    var runtimePackages = new HashSet<NugetPackage>();
                    var targetAlias = TargetFrameworkToAlias(targetRuntime.TargetFramework);
                    var dependenciesProperties = default(JsonElement);
                    var hasDependencyProperties = frameworksProperties.TryGetProperty(targetAlias, out var frameworkProperties)
                                          && frameworkProperties.TryGetProperty("dependencies", out dependenciesProperties);

                    foreach (var library in targetRuntime.Libraries.Where(lib => lib.Type != "project"))
                    {
                        var packageProperties = default(JsonElement);
                        var hasPackageProperties = hasDependencyProperties
                                                   && dependenciesProperties.TryGetProperty(library.Name, out packageProperties);

                        var package = new NugetPackage
                        {
                            Name = library.Name,
                            Version = library.Version.ToNormalizedString(),
                            Scope = Component.ComponentScope.Required,
                            Dependencies = new Dictionary<string, string>(),
                            // get value from project.assets.json file ( x."project"."frameworks".<framework>."dependencies".<library.Name>."suppressParent") 
                            IsDevDependency = hasPackageProperties && SetIsDevDependency(packageProperties),
                            IsDirectReference = hasPackageProperties && SetIsDirectReference(packageProperties)
                        };

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

                    ResolveDependencyVersionRanges(runtimePackages);

                    packages.UnionWith(runtimePackages);
                }
            }

            return packages;
        }
        public bool SetIsDirectReference(JsonElement jsonContent)
        {
            // every direct reference has target property
            return jsonContent.TryGetProperty("target", out _);
        }
        public bool SetIsDevDependency(JsonElement jsonContent)
        {
            // suppressParent: exists only for development dependencies
            return jsonContent.TryGetProperty("suppressParent", out _);
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
        private string TargetFrameworkToAlias(NuGetFramework framework)
        {
            var target = framework.ToString();
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
        private static void ResolveDependencyVersionRanges(HashSet<NugetPackage> runtimePackages)
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
