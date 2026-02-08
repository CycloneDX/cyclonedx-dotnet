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
                    var targetFramework = assetsFile.PackageSpec.GetTargetFramework(targetRuntime.TargetFramework);
                    var dependencies = targetFramework.Dependencies;
                    var directDependencies = assetsFile.ProjectFileDependencyGroups
                        .Where(f => f.FrameworkName == targetRuntime.Name)?.SelectMany(p => p.Dependencies)
                        .Select(d =>
                        {
                            var x = d.Split(" ");
                            return new { Name = x.First() };

                        });

                    foreach (var lockFileLibrary in targetRuntime.Libraries)
                    {
                        var libs = dependencies.FirstOrDefault(ld => ld.Name.Equals(lockFileLibrary.Name));
                        //try to find dependency in the library section, to get its path
                        var library = assetsFile.Libraries.FirstOrDefault(lib => lib.Name == lockFileLibrary.Name && lib.Version == lockFileLibrary.Version);

                        var package = new DotnetDependency
                        {
                            Name = lockFileLibrary.Name,
                            Version = lockFileLibrary.Version.ToNormalizedString(),
                            Scope = Component.ComponentScope.Required,
                            Dependencies = new Dictionary<string, string>(),
                            IsDevDependency = SetIsDevDependency(libs),
                            IsDirectReference = directDependencies?.Any(d => string.Compare(d.Name, lockFileLibrary.Name, true) == 0) ?? false,                            
                            DependencyType = (lockFileLibrary.Type != "project") ? DependencyType.Package : DependencyType.Project,
                            Path = Path.Combine(Path.GetDirectoryName(projectFilePath), library?.Path ?? "")
                        };

                        // is this a test project dependency or only a development dependency
                        if ( isTestProject)
                        {
                            package.Scope = Component.ComponentScope.Excluded;
                        }
                        // include direct dependencies
                        foreach (var dep in lockFileLibrary.Dependencies)
                        {
                            package.Dependencies.Add(dep.Id, dep.VersionRange?.ToNormalizedString());
                        }
                        runtimePackages.Add(package);
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
