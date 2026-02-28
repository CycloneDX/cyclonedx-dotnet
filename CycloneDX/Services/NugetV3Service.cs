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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CycloneDX.Services
{
    /// <summary>
    /// Based on https://docs.microsoft.com/en-us/nuget/reference/nuget-client-sdk
    /// </summary>
    public class NugetV3Service : INugetService
    {
        private readonly SourceRepository _sourceRepository;
        private readonly SourceCacheContext _sourceCacheContext;
        private readonly CancellationToken _cancellationToken;
        private readonly ILogger _logger;

        private readonly IGithubService _githubService;
        private readonly IFileSystem _fileSystem;
        private readonly List<string> _packageCachePaths;
        private readonly bool _disableHashComputation;

        // Used in local files
        private const string _nuspecExtension = ".nuspec";
        private const string _nupkgExtension = ".nupkg";
        private const string _sha512Extension = ".nupkg.sha512";

        public NugetV3Service(
            NugetInputModel nugetInput,
            IFileSystem fileSystem,
            List<string> packageCachePaths,
            IGithubService githubService,
            ILogger logger,
            bool disableHashComputation
        )
        {
            _fileSystem = fileSystem;
            _packageCachePaths = packageCachePaths;
            _githubService = githubService;
            _disableHashComputation = disableHashComputation;
            _logger = logger;

            _sourceRepository = SetupNugetRepository(nugetInput);
            _sourceCacheContext = new SourceCacheContext();
            _cancellationToken = CancellationToken.None;
        }

        internal string GetCachedNuspecFilename(string name, string version)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) { return null; }

            var lowerName = name.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            string nuspecFilename = null;

            foreach (var packageCachePath in _packageCachePaths)
            {
                var currentDirectory = _fileSystem.Path.Combine(packageCachePath, lowerName, NormalizeVersion(lowerVersion));
                var currentFilename = _fileSystem.Path.Combine(currentDirectory, lowerName + _nuspecExtension);
                if (_fileSystem.File.Exists(currentFilename))
                {
                    nuspecFilename = currentFilename;
                    break;
                }
            }

            return nuspecFilename;
        }

        /// <summary>
        /// Normalize the version string according to
        /// https://learn.microsoft.com/en-us/nuget/concepts/package-versioning#normalized-version-numbers
        /// </summary>
        private string NormalizeVersion(string version)
        {
            var separator = Math.Max(version.IndexOf('-'), version.IndexOf('+'));
            var part1 = separator < 0 ? version : version.Substring(0, separator);
            var part2 = separator < 0 ? string.Empty : version.Substring(separator);
            if (Version.TryParse(part1, out var parsed) && parsed.Revision == 0)
            {
                part1 = parsed.ToString(3);
                version = part1 + part2;
            }

            return version;
        }

        /// <summary>
        /// Converts Git SCP-like URLs to IRI / .NET URI parse-able equivalent.
        /// </summary>
        /// <param name="input">The VCS URI to normalize.</param>
        /// <returns>A string parseable by Uri.Parse, otherwise null.</returns>
        private string NormalizeUri(string input)
        {
            const string FALLBACK_SCHEME = "https://";

            if (string.IsNullOrWhiteSpace(input)) { return null; }

            UriCreationOptions ops = new UriCreationOptions();

            input = input.Trim();

            if (Uri.TryCreate(input, ops, out var result))
            {
                return result.ToString();
            }

            // Locate the main parts of the 'SCP-like' Git URL
            // https://git-scm.com/docs/git-clone#_git_urls
            // 1. Optional user
            // 2. Host
            // 3. Path
            int colonLocation = input.IndexOf(':');
            if (colonLocation == -1)
            {
                // Uri.Parse can fail in the absense of colons AND the absense of a scheme.
                // Add the fallback scheme to see if Uri.Parse can then interpret.
                return NormalizeUri($"{FALLBACK_SCHEME}{input}");
            }

            var userAndHostPart = input.Substring(0, colonLocation);
            var pathPart = input.Substring(colonLocation + 1, input.Length - 1 - userAndHostPart.Length);

            var tokens = userAndHostPart.Split('@');
            if (tokens.Length != 2)
            {
                // More than 1 @ would be invalid. No @ would probably have parsed ok by .NET.
                return null;
            }

            var user = tokens[0];
            var host = tokens[1];

            var sb = new StringBuilder();
            sb.Append(FALLBACK_SCHEME); // Assume this is the scheme which caused the parsing issue.

            if (!string.IsNullOrEmpty(user))
            {
                sb.Append(user);
                sb.Append(":@");
            }

            sb.Append(host);

            if (!pathPart.StartsWith('/'))
            {
                sb.Append("/");
            }

            sb.Append(pathPart);

            if (Uri.TryCreate(sb.ToString(), ops, out var adapted))
            {
                return adapted.ToString();
            }

            return null;
        }

        private SourceRepository SetupNugetRepository(NugetInputModel nugetInput)
        {
            if (nugetInput == null || string.IsNullOrEmpty(nugetInput.nugetFeedUrl) ||
                string.IsNullOrEmpty(nugetInput.nugetUsername) || string.IsNullOrEmpty(nugetInput.nugetPassword))
            {
                return Repository.Factory.GetCoreV3(nugetInput?.nugetFeedUrl ?? "https://api.nuget.org/v3/index.json");
            }

            var packageSource =
                GetPackageSourceWithCredentials(nugetInput);
            return Repository.Factory.GetCoreV3(packageSource);
        }

        private PackageSource GetPackageSourceWithCredentials(NugetInputModel nugetInput)
        {
            var packageSource = new PackageSource(nugetInput.nugetFeedUrl)
            {
                Credentials = new PackageSourceCredential(nugetInput.nugetFeedUrl, nugetInput.nugetUsername,
                    nugetInput.nugetPassword,
                    nugetInput.IsPasswordClearText, null)
            };

            return packageSource;
        }

        private static byte[] ComputeSha215Hash(Stream stream)
        {
            using (SHA512 sha = SHA512.Create())
            {
                return sha.ComputeHash(stream);
            }
        }

        private Component SetupComponent(string name, string version, Component.ComponentScope? scope)
        {
            var component = new Component
            {
                Name = name,
                Version = version,
                Scope = scope,
                Purl = Utils.GeneratePackageUrl(name, version),
                Type = Component.Classification.Library
            };

            component.BomRef = component.Purl;
            return component;
        }

        public async Task<Component> GetComponentAsync(string name, string version, Component.ComponentScope? scope)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) { return null; }

            // https://docs.microsoft.com/en-us/nuget/reference/nuget-client-sdk - Download a package
            var resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            var component = SetupComponent(name, version, scope);
            var nuspecFilename = GetCachedNuspecFilename(name, version);
            var nuspecModel = await GetNuspec(name, version, nuspecFilename, resource).ConfigureAwait(false);
            if (nuspecModel.hashBytes != null)
            {
                var hex = BitConverter.ToString(nuspecModel.hashBytes).Replace("-", string.Empty);
                Hash h = new Hash { Alg = Hash.HashAlgorithm.SHA_512, Content = hex };
                component.Hashes = new List<Hash> { h };
            }

            if (nuspecModel.nuspecReader == null) { return component; }

            component = SetupComponentProperties(component, nuspecModel);

            var licenseMetadata = nuspecModel.nuspecReader.GetLicenseMetadata();
            if (licenseMetadata != null && licenseMetadata.Type == LicenseType.Expression)
            {
                Action<NuGetLicense> licenseProcessor = delegate (NuGetLicense nugetLicense)
                {
                    var license = new License();
                    license.Id = nugetLicense.Identifier;
                    license.Name = license.Id == null ? nugetLicense.Identifier : null;
                    component.Licenses ??= new List<LicenseChoice>();
                    component.Licenses.Add(new LicenseChoice { License = license });
                };
                licenseMetadata.LicenseExpression.OnEachLeafNode(licenseProcessor, null);
            }
            else if (_githubService == null)
            {
                var licenseUrl = nuspecModel.nuspecReader.GetLicenseUrl();
                var license = new License { Name = "Unknown - See URL", Url = licenseUrl?.Trim() };
                component.Licenses = new List<LicenseChoice> { new LicenseChoice { License = license } };
            }
            else
            {
                License license = null;
                var licenseUrl = nuspecModel.nuspecReader.GetLicenseUrl();
                if (!string.IsNullOrEmpty(licenseUrl))
                {
                    license = await _githubService.GetLicenseAsync(licenseUrl).ConfigureAwait(false);
                }

                if (license == null)
                {
                    // try repository URLs for potential that they are github
                    var repository = nuspecModel.nuspecReader.GetRepositoryMetadata();
                    if (repository != null && !string.IsNullOrWhiteSpace(repository.Url))
                    {
                        if (!string.IsNullOrWhiteSpace(repository.Commit) && IsSafeCommitRef(repository.Commit))
                        {
                            license = await _githubService.GetLicenseAsync($"{repository.Url}/blob/{repository.Commit}/licence").ConfigureAwait(false);
                        }

                        license ??= await _githubService.GetLicenseAsync(repository.Url).ConfigureAwait(false);
                    }
                }

                if (license == null)
                {
                    // try project URL for potential that they are github
                    var project = nuspecModel.nuspecReader.GetProjectUrl();
                    if (!string.IsNullOrWhiteSpace(project))
                    {
                        license = await _githubService.GetLicenseAsync(project).ConfigureAwait(false);
                    }
                }

                if (license != null)
                {
                    component.Licenses = new List<LicenseChoice> { new LicenseChoice { License = license } };
                }
            }

            var projectUrl = nuspecModel.nuspecReader.GetProjectUrl();
            if (!string.IsNullOrEmpty(projectUrl))
            {
                var externalReference = new ExternalReference
                {
                    Type = ExternalReference.ExternalReferenceType.Website,
                    Url = projectUrl
                };
                component.ExternalReferences = new List<ExternalReference> { externalReference };
            }

            // Source: https://docs.microsoft.com/de-de/nuget/reference/nuspec#repository
            var repoMeta = nuspecModel.nuspecReader.GetRepositoryMetadata();
            var vcsUrl = NormalizeUri(repoMeta?.Url);
            if (!string.IsNullOrEmpty(vcsUrl))
            {
                var externalReference = new ExternalReference
                {
                    Type = ExternalReference.ExternalReferenceType.Vcs,
                    Url = vcsUrl
                };
                if (null == component.ExternalReferences)
                {
                    component.ExternalReferences = new List<ExternalReference> { externalReference };
                }
                else
                {
                    component.ExternalReferences.Add(externalReference);
                }
            }

            return component;
        }

        private static Component SetupComponentProperties(Component component, NuspecModel nuspecModel)
        {
            component.Authors = new List<OrganizationalContact> { new OrganizationalContact { Name = nuspecModel.nuspecReader.GetAuthors() } };
            component.Copyright = nuspecModel.nuspecReader.GetCopyright();
            // this prevents empty copyright values in the JSON BOM
            if (string.IsNullOrEmpty(component.Copyright))
            {
                component.Copyright = null;
            }

            var title = nuspecModel.nuspecReader.GetTitle();
            var summary = nuspecModel.nuspecReader.GetSummary();
            var description = nuspecModel.nuspecReader.GetDescription();
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

            return component;
        }

        private async Task<NuspecModel> GetNuspec(string name, string version, string nuspecFilename,
            FindPackageByIdResource resource)
        {
            var nuspecModel = new NuspecModel();
            if (nuspecFilename == null)
            {
                var packageVersion = new NuGetVersion(version);
                await using MemoryStream packageStream = new MemoryStream();
                await resource.CopyNupkgToStreamAsync(name, packageVersion, packageStream, _sourceCacheContext,
                    _logger, _cancellationToken);

                try
                {
                    using PackageArchiveReader packageReader = new PackageArchiveReader(packageStream);
                    nuspecModel.nuspecReader = await packageReader.GetNuspecReaderAsync(_cancellationToken);

                    if (!_disableHashComputation)
                    {
                        nuspecModel.hashBytes = ComputeSha215Hash(packageStream);
                    }
                }
                catch (InvalidDataException)
                {
                    Console.Error.WriteLine($"Unable to extract the nuget package: {name} - {version}");
                    throw;
                }
            }
            else
            {
                await using (var xmlStream = _fileSystem.File.OpenRead(nuspecFilename))
                {
                    nuspecModel.nuspecReader = new NuspecReader(xmlStream);
                }

                // reference: https://docs.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-add
                // and: https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File
                //  └─<packageID>
                //   └─<version>
                //    ├─<packageID>.<version>.nupkg
                //    ├─<packageID>.<version>.nupkg.sha512
                //    └─<packageID>.nuspec

                var normalizedVersion = NormalizeVersion(version);
                string shaFilename = Path.ChangeExtension(nuspecFilename, normalizedVersion + _sha512Extension);
                string nupkgFilename = Path.ChangeExtension(nuspecFilename, normalizedVersion + _nupkgExtension);

                if (_fileSystem.File.Exists(shaFilename))
                {
                    string base64Hash = _fileSystem.File.ReadAllText(shaFilename);
                    nuspecModel.hashBytes = Convert.FromBase64String(base64Hash);
                }
                else if (!_disableHashComputation && _fileSystem.File.Exists(nupkgFilename))
                {
                    await using (var nupkgStream = _fileSystem.File.OpenRead(nupkgFilename))
                    {
                        nuspecModel.hashBytes = ComputeSha215Hash(nupkgStream);
                    }
                }
            }

            return nuspecModel;
        }

        public async Task<Component> GetComponentAsync(DotnetDependency DotnetDependency)
        {
            Contract.Requires(DotnetDependency != null);
            return await GetComponentAsync(DotnetDependency.Name, DotnetDependency.Version, DotnetDependency.Scope)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Returns true if <paramref name="commitRef"/> contains only characters that are safe to
        /// interpolate into a URL path segment (hex digits, letters, digits, hyphens, underscores
        /// and dots). This rejects values that contain URL metacharacters such as '?', '#', '%',
        /// '@', ':', '/', or whitespace that could be used to inject query strings or path
        /// traversal sequences into downstream HTTP requests.
        /// </summary>
        internal static bool IsSafeCommitRef(string commitRef)
        {
            if (string.IsNullOrEmpty(commitRef)) return false;
            foreach (var c in commitRef)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                    return false;
            }
            return true;
        }
    }
}
