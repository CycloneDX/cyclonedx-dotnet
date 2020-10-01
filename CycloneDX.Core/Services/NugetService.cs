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
using System.Diagnostics.Contracts;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Packaging.Licenses;
using NuspecReader = NuGet.Packaging.NuspecReader;
using CycloneDX.Extensions;
using CycloneDX.Models;
using CycloneDX.Core.Models;

namespace CycloneDX.Services
{
    public interface INugetService
    {
        Task<Component> GetComponentAsync(string name, string version, string scope);
        Task<Component> GetComponentAsync(NugetPackage nugetPackage);
    }

    public class NugetService : INugetService
    {
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;
        private readonly IGithubService _githubService;
        private readonly IFileSystem _fileSystem;
        private readonly List<string> _packageCachePaths;

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
            _baseUrl = baseUrl ?? "https://api.nuget.org/v3-flatcontainer/";
        }

        internal string GetCachedNuspecFilename(string name, string version)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) return null;

            var lowerName = name.ToLowerInvariant();

            return _packageCachePaths.Select(packageCachePath => _fileSystem.Path.Combine(packageCachePath, lowerName, version)).Select(currentDirectory => _fileSystem.Path.Combine(currentDirectory, lowerName + ".nuspec")).FirstOrDefault(currentFilename => _fileSystem.File.Exists(currentFilename));
        }

        /// <summary>
        /// Retrieves the specified component from NuGet.
        /// </summary>
        /// <param name="name">NuGet package name</param>
        /// <param name="version">Package version</param>
        /// <returns></returns>
        public async Task<Component> GetComponentAsync(string name, string version, string scope)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) return null;

            Console.WriteLine("Retrieving " + name + " " + version);

            var component = new Component
            {
                Name = name,
                Version = version,
                Scope = scope,
                Purl = Utils.GeneratePackageUrl(name, version)
            };

            var nuspecFilename = GetCachedNuspecFilename(name, version);

            NuspecReader nuspecReader = null;

            if (nuspecFilename == null)
            {
                var url = _baseUrl + name + "/" + version + "/" + name + ".nuspec";
                using (var xmlStream = await _httpClient.GetXmlStreamAsync(url).ConfigureAwait(false))
                {
                    if (xmlStream != null) nuspecReader = new NuspecReader(xmlStream);
                }
            }
            else
            {
                using (var xmlStream = _fileSystem.File.Open(nuspecFilename, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    nuspecReader = new NuspecReader(xmlStream);
                }
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
                void LicenseProcessor(NuGetLicense nugetLicense)
                {
                    var license = new License {Id = nugetLicense.Identifier, Name = nugetLicense.Identifier};
                    component.Licenses.Add(new ComponentLicense {License = license});
                }

                licenseMetadata.LicenseExpression.OnEachLeafNode(LicenseProcessor, null);
            }
            else
            {
                var licenseUrl = nuspecReader.GetLicenseUrl();
                if (!string.IsNullOrEmpty(licenseUrl))
                {
                    License license = null;
                    
                    if (_githubService != null)
                    {
                        license = await _githubService.GetLicenseAsync(licenseUrl).ConfigureAwait(false);
                    }

                    if (license == null)
                    {
                        license = new License
                        {
                            Url = licenseUrl
                        };
                    }
                    
                    component.Licenses.Add(new ComponentLicense
                    {
                        License = license
                    });
                }
            }

            var projectUrl = nuspecReader.GetProjectUrl();
            if (projectUrl == null) 
                return component;

            var externalReference = new ExternalReference
            {
                Type = ExternalReference.WEBSITE, 
                Url = projectUrl
            };
            component.ExternalReferences.Add(externalReference);

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