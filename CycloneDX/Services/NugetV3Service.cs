using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Security.Cryptography;
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
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) {return null;}

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

        private SourceRepository SetupNugetRepository(NugetInputModel nugetInput)
        {
            if (nugetInput == null || string.IsNullOrEmpty(nugetInput.nugetFeedUrl) ||
                string.IsNullOrEmpty(nugetInput.nugetUsername) || string.IsNullOrEmpty(nugetInput.nugetPassword))
            {
                return Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
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
            Console.WriteLine("Retrieving " + name + " " + version);

            var component = SetupComponent(name, version, scope);
            var nuspecFilename = GetCachedNuspecFilename(name, version);
            NuspecReader nuspecReader;
            byte[] hashBytes = null;

            if (nuspecFilename == null)
            {
                var packageVersion = new NuGetVersion(version);
                await using MemoryStream packageStream = new MemoryStream();
                await resource.CopyNupkgToStreamAsync(name, packageVersion, packageStream, _sourceCacheContext,
                    _logger, _cancellationToken);

                Console.WriteLine($"Downloaded package {name} {packageVersion}");
                using PackageArchiveReader packageReader = new PackageArchiveReader(packageStream);
                nuspecReader = await packageReader.GetNuspecReaderAsync(_cancellationToken);


                if (!_disableHashComputation)
                {
                    hashBytes = ComputeSha215Hash(packageStream);
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
                else if (!_disableHashComputation && _fileSystem.File.Exists(nupkgFilename))
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
                Hash h = new Hash
                {
                    Alg = Hash.HashAlgorithm.SHA_512,
                    Content = hex
                };
                component.Hashes = new List<Hash> { h };
            }

            if (nuspecReader == null){ return component;}

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
                    var license = new License
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
                var externalReference = new ExternalReference();
                externalReference.Type = ExternalReference.ExternalReferenceType.Website;
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
                var externalReference = new ExternalReference();
                externalReference.Type = ExternalReference.ExternalReferenceType.Vcs;
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

        public async Task<Component> GetComponentAsync(NugetPackage nugetPackage)
        {
            Contract.Requires(nugetPackage != null);
            return await GetComponentAsync(nugetPackage.Name, nugetPackage.Version, nugetPackage.Scope)
                .ConfigureAwait(false);
        }
    }
}
