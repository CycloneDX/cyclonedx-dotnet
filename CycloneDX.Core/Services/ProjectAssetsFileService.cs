// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

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

        public HashSet<NugetPackage> GetNugetPackages(string projectAssetsFilePath)
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
                        var package = new NugetPackage
                        {
                            Name = library.Name,
                            Version = library.Version.ToNormalizedString(),
                            Scope = "required",
                        };
                        // is this only a development dependency
                        if (
                            library.CompileTimeAssemblies.Count == 0
                            && library.ContentFiles.Count == 0
                            && library.EmbedAssemblies.Count == 0
                            && library.FrameworkAssemblies.Count == 0
                            && library.NativeLibraries.Count == 0
                            && library.ResourceAssemblies.Count == 0
                            && library.ToolsAssemblies.Count == 0
                        )
                        {
                            package.Scope = "excluded";
                        }
                        packages.Add(package);
                    }
                }
            }

            return packages;
        }
    }
}