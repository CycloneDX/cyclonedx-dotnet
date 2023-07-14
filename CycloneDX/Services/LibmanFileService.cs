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
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class LibmanFileService : ILibmanFileService
    {
        private readonly IFileSystem _fileSystem;

        public LibmanFileService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public async Task<HashSet<BasePackage>> GetLibmanPackagesAsync(string libmanFilePath)
        {
            var packages = new HashSet<BasePackage>();

            try
            {
                using var fileReader = _fileSystem.File.OpenText(libmanFilePath);
                var model = await JsonSerializer.DeserializeAsync<LibmanModel>(fileReader.BaseStream).ConfigureAwait(false);

                foreach (var library in model.Libraries)
                {
                    var provider = library.Provider ?? model.DefaultProvider;

                    packages.Add(new LibmanPackage(provider)
                    {
                        IsDirectReference = true,
                        Name = library.Name,
                        Namespace = library.Namespace,
                        Version = library.Version
                    });
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error reading {libmanFilePath}: {ex.Message}.");
            }

            return packages;
        }
    }
}
