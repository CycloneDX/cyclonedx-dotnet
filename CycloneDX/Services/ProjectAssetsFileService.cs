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
                var assetFileReader = _assetFileReaderFactory();
                var assetsFile = assetFileReader.Read(projectAssetsFilePath);

                foreach (var targetRuntime in assetsFile.Targets)
                {
                    var runtimePackages = new HashSet<NugetPackage>();
                    var targetFramework = assetsFile.PackageSpec.GetTargetFramework(targetRuntime.TargetFramework);
                    var dependencies = targetFramework.Dependencies;

                    foreach (var library in targetRuntime.Libraries.Where(lib => lib.Type != "project"))
                    {
                        var libs = dependencies.FirstOrDefault(ld => ld.Name.Equals(library.Name));
                        var package = new NugetPackage
                        {
                            Name = library.Name,
                            Version = library.Version.ToNormalizedString(),
                            Scope = Component.ComponentScope.Required,
                            Dependencies = new Dictionary<string, string>(),
                            IsDevDependency = SetIsDevDependency(libs),
                            IsDirectReference = SetIsDirectReference(libs)
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
        public bool SetIsDirectReference(LibraryDependency dependency)
        {
            return dependency?.ReferenceType == LibraryDependencyReferenceType.Direct;
        }
        public bool SetIsDevDependency(LibraryDependency dependency)
        {
            return dependency != null && dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent;
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
