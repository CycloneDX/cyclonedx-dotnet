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
using System.Xml;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Services
{
    public class PackagesFileService
    {
        private XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings 
        {
            Async = true
        };
        
        private IFileSystem _fileSystem;
        private FileDiscoveryService _fileDiscoveryService;

        public PackagesFileService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _fileDiscoveryService = new FileDiscoveryService(fileSystem);
        }

        /// <summary>
        /// Analyzes a single packages.config file for NuGet package references.
        /// </summary>
        /// <param name="packagesFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> GetNugetPackagesAsync(string packagesFilePath)
        {
            var packages = new HashSet<NugetPackage>();
            using (StreamReader fileReader = _fileSystem.File.OpenText(packagesFilePath))
            {
                using (XmlReader reader = XmlReader.Create(fileReader, _xmlReaderSettings))
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.IsStartElement() && reader.Name == "package")
                        {
                            packages.Add(new NugetPackage
                            {
                                Name = reader["id"],
                                Version = reader["version"],
                            });
                        }
                    }
                }
            }
            return packages;
        }

        /// <summary>
        /// Recursively searches a directory and analyzes all packages.config files for NuGet package references.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> RecursivelyGetNugetPackagesAsync(string directoryPath)
        {
            var packages = new HashSet<NugetPackage>();
            var packageFiles = _fileDiscoveryService.GetPackagesConfigFiles(directoryPath);

            foreach (var packageFile in packageFiles)
            {
                var newPackages = await GetNugetPackagesAsync(packageFile);
                packages.UnionWith(newPackages);
            }

            return packages;
        }
    }
}