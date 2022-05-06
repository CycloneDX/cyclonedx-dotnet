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
using AssetFileReader = NuGet.ProjectModel.LockFileFormat;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class ProjectAssetsFileService : IProjectAssetsFileService 
    {
        private IFileSystem _fileSystem;

        public ProjectAssetsFileService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public HashSet<NugetPackage> GetNugetPackages(string projectAssetsFilePath, bool isTestProject)
        {
            var packages = new HashSet<NugetPackage>();

            if (_fileSystem.File.Exists(projectAssetsFilePath))
            {
                var assetFileReader = new AssetFileReader();
                var assetsFile = assetFileReader.Read(projectAssetsFilePath);

                foreach (var targetRuntime in assetsFile.Targets)
                {
                    foreach (var library in targetRuntime.Libraries)
                    {
                        if (library.Type != "project")
                        {
                            var package = new NugetPackage
                            {
                                Name = library.Name,
                                Version = library.Version.ToNormalizedString(),
                                Scope = Component.ComponentScope.Required,
                                Dependencies = new Dictionary<string, string>(),
                            };
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
                                //Get the version from the nuget package as described here: https://github.com/NuGet/NuGet.Client/blob/ad81306fe7ada265cf44afb2a60a31fbfca978a2/src/NuGet.Core/NuGet.ProjectModel/JsonUtility.cs#L54
                                package.Dependencies.Add(dep.Id, dep.VersionRange?.ToLegacyShortString());
                            }
                            packages.Add(package);
                        }
                    }
                }
            }

            return packages;
        }
    }
}
