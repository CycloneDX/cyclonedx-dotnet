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
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NuGet.Packaging.Licenses;
using NuspecReader = NuGet.Packaging.NuspecReader;
using CycloneDX.Models;
using CycloneDX.Extensions;
using CycloneDX.Models.v1_3;

namespace CycloneDX.Services
{
    public interface INugetService
    {
        Task<Component> GetComponentAsync(string name, string version, Component.ComponentScope? scope);
        Task<Component> GetComponentAsync(NugetPackage nugetPackage);
    }

    public class NugetService : INugetService
    {
        private string _baseUrl;
        private HttpClient _httpClient;
        private IGithubService _githubService;
        private IFileSystem _fileSystem;
        private List<string> _packageCachePaths;
        private const string _nuspecExtension = ".nuspec";
        private const string _nupkgExtension = ".nupkg";
        private const string _sha512Extension = ".nupkg.sha512";


        public NugetService(
            IFileSystem fileSystem,
            List<string> packageCachePaths,
            IGithubService githubService,
            HttpClient httpClient,
            string baseUrl = null)
        {
            _fileSystem = fileSystem;
            _packageCachePaths = packageCachePaths;
            _githubService = githubService;
            _httpClient = httpClient;
            _baseUrl = baseUrl == null ? "https://api.nuget.org/v3-flatcontainer/" : baseUrl;
        }

        internal string GetCachedNuspecFilename(string name, string version)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) return null;

            var lowerName = name.ToLowerInvariant();
            string nuspecFilename = null;

            foreach (var packageCachePath in _packageCachePaths)
            {
                var currentDirectory = _fileSystem.Path.Combine(packageCachePath, lowerName, version);
                var currentFilename = _fileSystem.Path.Combine(currentDirectory, lowerName + _nuspecExtension);
                if (_fileSystem.File.Exists(currentFilename))
                {
                    nuspecFilename = currentFilename;
                    break;
                }
            }

            return nuspecFilename;
        }

        private static byte[] ComputeSha215Hash(Stream stream)
        {
            using (SHA512 sha = new SHA512Managed())
            {
                return sha.ComputeHash(stream);
            }
        }

        /// <summary>
        /// Retrieves the specified component from NuGet.
        /// </summary>
        /// <param name="name">NuGet package name</param>
        /// <param name="version">Package version</param>
        /// <returns></returns>
        public async Task<Component> GetComponentAsync(string name, string version, Component.ComponentScope? scope)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) return null;

            Console.WriteLine("Retrieving " + name + " " + version);

            var component = new Component
            {
                Name = name,
                Version = version,
                Scope = scope,
                Purl = Utils.GeneratePackageUrl(name, version),
                Type = Component.Classification.Library
            };

            component.BomRef = component.Purl;

            var nuspecFilename = GetCachedNuspecFilename(name, version);

            NuspecReader nuspecReader = null;
            byte[] hashBytes = null;

            if (nuspecFilename == null)
            {
                var nugetUrlPrefix = _baseUrl + name + "/" + version + "/" + name;
                var nuspecUrl = nugetUrlPrefix + _nuspecExtension;
                var nupkgUrl = nugetUrlPrefix + "." + version + _nupkgExtension;
                using (var xmlStream = await _httpClient.GetStreamWithStatusCheckAsync(nuspecUrl).ConfigureAwait(false))
                {
                    if (xmlStream != null) nuspecReader = new NuspecReader(xmlStream);
                }

                using (var stream = await _httpClient.GetStreamWithStatusCheckAsync(nupkgUrl).ConfigureAwait(false))
                {
                    if (stream != null) hashBytes = ComputeSha215Hash(stream);
                }
            }
            else
            {
                using (var xmlStream = _fileSystem.File.OpenRead(nuspecFilename))
                {
                    nuspecReader = new NuspecReader(xmlStream);
                }

                // reference: https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-add
                // and: https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File
                //  └─<packageID>
                //   └─<version>
                //    ├─<packageID>.<version>.nupkg
                //    ├─<packageID>.<version>.nupkg.sha512
                //    └─<packageID>.nuspec

                string shaFilename = Path.ChangeExtension(nuspecFilename, version + _sha512Extension);
                string nupkgFilename = Path.ChangeExtension(nuspecFilename, version + _nupkgExtension);

                if (_fileSystem.File.Exists(shaFilename))
                {
                    string base64Hash = _fileSystem.File.ReadAllText(shaFilename);
                    hashBytes = Convert.FromBase64String(base64Hash);
                }
                else if (_fileSystem.File.Exists(nupkgFilename))
                {
                    using (var nupkgStream = _fileSystem.File.OpenRead(nupkgFilename))
                    {
                        hashBytes = ComputeSha215Hash(nupkgStream);
                    }
                }
            }

            if (hashBytes != null)
            {
                var hex = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                Hash h = new Hash()
                {
                    Alg = Hash.HashAlgorithm.SHA_512,
                    Content = hex
                };
                component.Hashes = new List<Hash>() { h };
            }

            if (nuspecReader == null) return component;

            component.Publisher = nuspecReader.GetAuthors();
            component.Copyright = nuspecReader.GetCopyright();
            // this prevents empty copyright values in the JSON BOM
            if (string.IsNullOrEmpty(component.Copyright))
            {
                component.Copyright = null;
            }
            var title = nuspecReader.GetTitle();
            var summary = nuspecReader.GetSummary();
            var description = nuspecReader.GetDescription();
            if (!string.IsNullOrEmpty(summary))
            {
                component.Description = summary;
            }
            else if (!string.IsNullOrEmpty(description))
            {
                component.Description = description;
            }
            else if (!string.IsNullOrEmpty(title))
            {
                component.Description = title;
            }

            var licenseMetadata = nuspecReader.GetLicenseMetadata();
            if (licenseMetadata != null && licenseMetadata.Type == NuGet.Packaging.LicenseType.Expression)
            {
                Action<NuGetLicense> licenseProcessor = delegate (NuGetLicense nugetLicense)
                {
                    var license = new Models.v1_3.License
                    {
                        Id = nugetLicense.Identifier,
                        Name = nugetLicense.Identifier
                    };
                    component.Licenses = new List<LicenseChoice>
                    {
                        new LicenseChoice
                        {
                            License = license
                        }
                    };
                };
                licenseMetadata.LicenseExpression.OnEachLeafNode(licenseProcessor, null);
            }
            else
            {
                var licenseUrl = nuspecReader.GetLicenseUrl();
                if (!string.IsNullOrEmpty(licenseUrl))
                {
                    Models.v1_3.License license = null;
                    
                    if (_githubService != null)
                    {
                        license = await _githubService.GetLicenseAsync(licenseUrl).ConfigureAwait(false);
                    }

                    if (license == null)
                    {
                        license = new Models.v1_3.License
                        {
                            Url = licenseUrl
                        };
                    }
                    
                    component.Licenses = new List<LicenseChoice>
                    {
                        new LicenseChoice
                        {
                            License = license
                        }
                    };
                }
            }

            var projectUrl = nuspecReader.GetProjectUrl();
            if (!string.IsNullOrEmpty(projectUrl))
            {
                var externalReference = new Models.v1_3.ExternalReference();
                externalReference.Type = Models.v1_3.ExternalReference.ExternalReferenceType.Website;
                externalReference.Url = projectUrl;
                component.ExternalReferences = new List<ExternalReference>
                {
                    externalReference
                };
            }

            // Source: https://docs.microsoft.com/de-de/nuget/reference/nuspec#repository
            var repoMeta = nuspecReader.GetRepositoryMetadata();
            var vcsUrl = repoMeta?.Url;
            if (!string.IsNullOrEmpty(vcsUrl))
            {
                var externalReference = new Models.v1_3.ExternalReference();
                externalReference.Type = Models.v1_3.ExternalReference.ExternalReferenceType.Vcs;
                externalReference.Url = vcsUrl;
                if (null == component.ExternalReferences)
                {
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        externalReference
                    };
                }
                else
                {
                    component.ExternalReferences.Add(externalReference);
                }
            }

            return component;
        }

        /// <summary>
        /// Retrieves the specified component from NuGet.
        /// </summary>
        /// <param name="package">NuGet package</param>
        /// <returns></returns>
        public async Task<Component> GetComponentAsync(NugetPackage package)
        {
            Contract.Requires(package != null);
            return await GetComponentAsync(package.Name, package.Version, package.Scope).ConfigureAwait(false);
        }
    }
}
