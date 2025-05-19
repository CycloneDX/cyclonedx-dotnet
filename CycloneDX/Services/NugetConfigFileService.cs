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

using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using System.Linq;
using System;

namespace CycloneDX.Services
{
    public class NugetConfigFileService : INugetConfigFileService
    {
        private readonly XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings 
        {
            Async = true
        };
        
        private readonly IFileSystem _fileSystem;
        private readonly FileDiscoveryService _fileDiscoveryService;

        public NugetConfigFileService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _fileDiscoveryService = new FileDiscoveryService(fileSystem);
        }

        /// <summary>
        /// Analyzes a single nuget.config file for package sources.
        /// </summary>
        /// <param name="configFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetInputModel>> GetPackageSourcesAsync(string configFilePath)
        {
            var sources = new HashSet<NugetInputModel>();
            using (StreamReader fileReader = _fileSystem.File.OpenText(configFilePath))
            {
                using (XmlReader reader = XmlReader.Create(fileReader, _xmlReaderSettings))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (reader.IsStartElement() && reader.Name == "add")
                        {
                            string packageSource = reader["value"];
                            if (packageSource.StartsWith("https://api.nuget.org/v3"))
                            {
                                // normalize default URL
                                packageSource = "https://api.nuget.org/v3/index.json";
                            }
                            var newSource = new NugetInputModel(packageSource);
                            newSource.nugetFeedName = reader["key"];

                            // TODO - user, password

                            await Console.Out.WriteLineAsync($"\tFound Package Source:{newSource.nugetFeedName}");
                            sources.Add(newSource);                            
                        }
                    }
                }
            }
            return sources;
        }

        /// <summary>
        /// Recursively searches a directory and analyzes all packages.config files for NuGet package references.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetInputModel>> RecursivelyGetPackageSourcesAsync(string directoryPath)
        {
            var sources = new HashSet<NugetInputModel>();
            var configFiles = _fileDiscoveryService.GetNugetConfigFiles(directoryPath);

            foreach (var configFile in configFiles)
            {
                var newsources = await GetPackageSourcesAsync(configFile).ConfigureAwait(false);
                sources.UnionWith(newsources);
            }

            return sources;
        }
    }
}
