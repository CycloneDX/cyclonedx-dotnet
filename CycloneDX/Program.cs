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
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using CycloneDX.Models;
using CycloneDX.Services;
using System.Reflection;
using System.Linq;
using CycloneDX.Interfaces;

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("CycloneDX.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("CycloneDX.IntegrationTests")]
namespace CycloneDX {
    [Command(Name = "dotnet cyclonedx", FullName = "A .NET Core global tool which creates CycloneDX Software Bill-of-Materials (SBOM) from .NET projects.")]
    class Program {
        #region Options
        [Option(Description = "Output the tool version and exit", ShortName = "v", LongName = "version")]
        bool version { get; }

        [Argument(0, Name = "path", Description = "The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files")]
        string SolutionOrProjectFile { get; set; }

        [Option(Description = "The directory to write the BOM", ShortName = "o", LongName = "out")]
        string outputDirectory { get; }

        [Option(Description = "Produce a JSON BOM instead of XML", ShortName = "j", LongName = "json")]
        bool json { get; }

        [Option(Description = "Exclude development dependencies from the BOM", ShortName = "d", LongName = "exclude-dev")]
        bool excludeDev { get; }

        [Option(Description = "Exclude test projects from the BOM", ShortName = "t", LongName = "exclude-test-projects")]
        bool excludetestprojects { get; }

        [Option(Description = "Alternative NuGet repository URL to https://<yoururl>/nuget/<yourrepository>/v3/index.json", ShortName = "u", LongName = "url")]
        string baseUrl { get; set; }

        [Option(Description = "Alternative NuGet repository username", ShortName = "us", LongName = "baseUrlUsername")]
        string baseUrlUserName { get; set; }

        [Option(Description = "Alternative NuGet repository username password/apikey", ShortName = "usp", LongName = "baseUrlUserPassword")]
        string baseUrlUserPassword { get; set; }

        [Option(Description = "Alternative NuGet repository password is cleartext", ShortName = "uspct", LongName = "isBaseUrlPasswordClearText")]
        bool isPasswordClearText { get; set; }

        [Option(Description = "To be used with a single project file, it will recursively scan project references of the supplied .csproj", ShortName = "r", LongName = "recursive")]
        bool scanProjectReferences { get; set; }

        [Option(Description = "DEPRECATED: Optionally omit the serial number from the resulting BOM", ShowInHelpText = false, ShortName = "nsdeprecated", LongName = "noSerialNumber")]
        bool noSerialNumberDeprecated { get; set; }
        [Option(Description = "Optionally omit the serial number from the resulting BOM", ShortName = "ns", LongName = "no-serial-number")]
        bool noSerialNumber { get; set; }

        [Option(Description = "DEPRECATED: Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token", ShowInHelpText = false, ShortName = "gudeprecated", LongName = "githubUsername")]
        string githubUsernameDeprecated { get; set; }
        [Option(Description = "Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token", ShortName = "gu", LongName = "github-username")]
        string githubUsername { get; set; }
        [Option(Description = "DEPRECATED: Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username", ShowInHelpText = false, ShortName = "gtdeprecated", LongName = "githubToken")]
        string githubTokenDeprecated { get; set; }
        [Option(Description = "Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username", ShortName = "gt", LongName = "github-token")]
        string githubToken { get; set; }
        [Option(Description = "DEPRECATED: Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions", ShowInHelpText = false, ShortName = "gbtdeprecated", LongName = "githubBearerToken")]
        string githubBearerTokenDeprecated { get; set; }
        [Option(Description = "Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions", ShortName = "gbt", LongName = "github-bearer-token")]
        string githubBearerToken { get; set; }
        [Option(Description = "DEPRECATED: Optionally disable GitHub license resolution", ShowInHelpText = false, ShortName = "dgldeprecated", LongName = "disableGithubLicenses")]
        bool disableGithubLicensesDeprecated { get; set; }
        [Option(Description = "Optionally disable GitHub license resolution", ShortName = "dgl", LongName = "disable-github-licenses")]
        bool disableGithubLicenses { get; set; }

        [Option(Description = "Optionally disable package restore", ShortName = "dpr", LongName = "disable-package-restore")]
        bool disablePackageRestore { get; set; }

        [Option(Description = "Optionally disable hash computation for packages", ShortName = "dhc", LongName = "disable-hash-computation")]
        bool disableHashComputation { get; set; }

        [Option(Description = "dotnet command timeout in milliseconds (primarily used for long dotnet restore operations)", ShortName = "dct", LongName = "dotnet-command-timeout")]
        int dotnetCommandTimeout { get; set; } = 300000;

        [Option(Description = "Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated.", ShortName = "biop", LongName = "base-intermediate-output-path")]
        public string baseIntermediateOutputPath { get; }

        [Option(Description = "Optionally provide a metadata template which has project specific details.", ShortName = "imp", LongName = "import-metadata-path")]
        public string importMetadataPath { get; }

        [Option(Description = "Override the autogenerated BOM metadata component name.", ShortName = "sn", LongName = "set-name")]
        public string setName { get; }

        [Option(Description = "Override the default BOM metadata component version (defaults to 0.0.0).", ShortName = "sv", LongName = "set-version")]
        public string setVersion { get; }

        [Option(Description = "Override the default BOM metadata component type (defaults to application).", ShortName = "st", LongName = "set-type")]
        public Component.Classification setType { get; }
#endregion options

        internal static IFileSystem fileSystem = new FileSystem();
        internal static readonly IDotnetCommandService dotnetCommandService = new DotnetCommandService();
        internal static readonly IProjectAssetsFileService projectAssetsFileService = new ProjectAssetsFileService(fileSystem, dotnetCommandService, () => new AssetFileReader());
        internal static readonly IDotnetUtilsService dotnetUtilsService = new DotnetUtilsService(fileSystem, dotnetCommandService);
        internal static readonly IPackagesFileService packagesFileService = new PackagesFileService(fileSystem);
        internal static readonly IProjectFileService projectFileService = new ProjectFileService(fileSystem, dotnetUtilsService, packagesFileService, projectAssetsFileService);
        internal static ISolutionFileService solutionFileService = new SolutionFileService(fileSystem, projectFileService);

        public static async Task<int> Main(string[] args)
            => await CommandLineApplication.ExecuteAsync<Program>(args).ConfigureAwait(false);

        async Task<int> OnExecuteAsync() {
            if (version)
            {
                Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString());
                return 0;
            }

            Console.WriteLine();

            // check parameter values
            if (string.IsNullOrEmpty(SolutionOrProjectFile)) {

                Console.Error.WriteLine($"A path is required");
                return (int)ExitCode.SolutionOrProjectFileParameterMissing;
            }

            if (string.IsNullOrEmpty(outputDirectory)) {
                Console.Error.WriteLine($"The output directory is required");
                return (int)ExitCode.OutputDirectoryParameterMissing;
            }

            if ((string.IsNullOrEmpty(githubUsername) ^ string.IsNullOrEmpty(githubToken))
                || (string.IsNullOrEmpty(githubUsernameDeprecated) ^ string.IsNullOrEmpty(githubTokenDeprecated)))
            {
                Console.Error.WriteLine($"Both GitHub username and token are required");
                return (int)ExitCode.GitHubParameterMissing;
            }

            dotnetCommandService.TimeoutMilliseconds = dotnetCommandTimeout;
            projectFileService.DisablePackageRestore = disablePackageRestore;

            // retrieve nuget package cache paths
            var packageCachePathsResult = dotnetUtilsService.GetPackageCachePaths();
            if (!packageCachePathsResult.Success)
            {
                Console.Error.WriteLine("Unable to find local package cache locations...");
                Console.Error.WriteLine(packageCachePathsResult.ErrorMessage);
                return (int)ExitCode.LocalPackageCacheError;
            }

            Console.WriteLine("Found the following local nuget package cache locations:");
            foreach (var path in packageCachePathsResult.Result)
            {
                Console.WriteLine($"    {path}");
            }

            // instantiate services

            var fileDiscoveryService = new FileDiscoveryService(Program.fileSystem);
            GithubService githubService = null;
            if (!(disableGithubLicenses || disableGithubLicensesDeprecated))
            {
                // GitHubService requires its own HttpClient as it adds a default authorization header
                if (!string.IsNullOrEmpty(githubBearerToken))
                {
                    githubService = new GithubService(new HttpClient(), githubBearerToken);
                }
                else if (!string.IsNullOrEmpty(githubBearerTokenDeprecated))
                {
                    githubService = new GithubService(new HttpClient(), githubBearerTokenDeprecated);
                }
                else if (!string.IsNullOrEmpty(githubUsername))
                {
                    githubService = new GithubService(new HttpClient(), githubUsername, githubToken);
                }
                else if (!string.IsNullOrEmpty(githubUsernameDeprecated))
                {
                    githubService = new GithubService(new HttpClient(), githubUsernameDeprecated, githubTokenDeprecated);
                }
                else
                {
                    githubService = new GithubService(new HttpClient());
                }
            }
            var nugetLogger = new NuGet.Common.NullLogger();
            var nugetInput =
                NugetInputFactory.Create(baseUrl, baseUrlUserName, baseUrlUserPassword, isPasswordClearText);
            var nugetService = new NugetV3Service(nugetInput, fileSystem, packageCachePathsResult.Result, githubService, nugetLogger, disableHashComputation);

            var packages = new HashSet<NugetPackage>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = Program.fileSystem.Path.GetFullPath(SolutionOrProjectFile);

            var topLevelComponent = new Component
            {
                // name is set below
                Version = string.IsNullOrEmpty(setVersion) ? "0.0.0" : setVersion,
                Type = setType == Component.Classification.Null ? Component.Classification.Application : setType,
            };

            try
            {
                if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    packages = await solutionFileService.GetSolutionNugetPackages(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
                {
                    packages = await projectFileService.RecursivelyGetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile))
                {
                    packages = await projectFileService.GetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Program.fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                {
                    packages = await packagesFileService.GetNugetPackagesAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetDirectoryName(fullSolutionOrProjectFilePath);
                }
                else if (fileSystem.Directory.Exists(fullSolutionOrProjectFilePath))
                {
                    packages = await packagesFileService.RecursivelyGetNugetPackagesAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetDirectoryName(fullSolutionOrProjectFilePath);
                }
                else
                {
                    Console.Error.WriteLine($"Only .sln, .csproj, .vbproj, and packages.config files are supported");
                    return (int)ExitCode.InvalidOptions;
                }
            }
            catch (DotnetRestoreException)
            {
                return (int)ExitCode.DotnetRestoreFailed;
            }

            if (!string.IsNullOrEmpty(setName))
            {
                topLevelComponent.Name = setName;
            }

            // get all the components and depdency graph from the NuGet packages
            var components = new HashSet<Component>();
            var dependencies = new List<Dependency>();
            var directDependencies = new Dependency { Dependencies = new List<Dependency>() };
            var transitiveDependencies = new HashSet<string>();
            var packageToComponent = new Dictionary<NugetPackage, Component>();
            try
            {
                var bomRefLookup = new Dictionary<(string,string), string>();
                foreach (var package in packages)
                {
                    var component = await nugetService.GetComponentAsync(package).ConfigureAwait(false);
                    if (component != null
                        && (component.Scope != Component.ComponentScope.Excluded || !excludeDev)
                    )
                    {
                        packageToComponent[package] = component;
                        components.Add(component);
                    }
                    bomRefLookup[(component.Name.ToLower(CultureInfo.InvariantCulture),(component.Version.ToLower(CultureInfo.InvariantCulture)))] = component.BomRef;
                }
                // now that we have all the bom ref lookups we need to enumerate all the dependencies
                foreach (var package in packages)
                {
                    var packageDepencies = new Dependency
                    {
                        Ref = bomRefLookup[(package.Name.ToLower(CultureInfo.InvariantCulture), package.Version.ToLower(CultureInfo.InvariantCulture))],
                        Dependencies = new List<Dependency>()
                    };
                    if (package.Dependencies != null)
                    {
                        foreach (var dep in package.Dependencies)
                        {
                            transitiveDependencies.Add(bomRefLookup[(dep.Key.ToLower(CultureInfo.InvariantCulture), dep.Value.ToLower(CultureInfo.InvariantCulture))]);
                            packageDepencies.Dependencies.Add(new Dependency
                            {
                                Ref = bomRefLookup[(dep.Key.ToLower(CultureInfo.InvariantCulture), dep.Value.ToLower(CultureInfo.InvariantCulture))]
                            });
                        }
                    }
                    dependencies.Add(packageDepencies);
                }
            }
            catch (InvalidGitHubApiCredentialsException)
            {
                return (int)ExitCode.InvalidGitHubApiCredentials;
            }
            catch (GitHubApiRateLimitExceededException)
            {
                return (int)ExitCode.GitHubApiRateLimitExceeded;
            }
            catch (GitHubLicenseResolutionException)
            {
                return (int)ExitCode.GitHubLicenseResolutionFailed;
            }

            var directPackageDependencies = packages.Where(p => p.IsDirectReference).Select(p => packageToComponent[p].BomRef).ToHashSet();
            // now we loop through all the dependencies to check which are direct
            foreach (var dep in dependencies)
            {
                if (directPackageDependencies.Contains((dep.Ref)) ||
                    (directPackageDependencies.Count == 0 && !transitiveDependencies.Contains(dep.Ref)))
                {
                    directDependencies.Dependencies.Add(new Dependency { Ref = dep.Ref });
                }
            }

            // create the BOM
            Console.WriteLine();
            Console.WriteLine("Creating CycloneDX BOM");
            var bom = new Bom
            {
                Version = 1,
            };

            if (!string.IsNullOrEmpty(importMetadataPath))
            {
                if (!File.Exists(importMetadataPath))
                {
                    Console.Error.WriteLine($"Metadata template '{importMetadataPath}' does not exist.");
                    return (int)ExitCode.InvalidOptions;
                }
                else
                {
                    bom = ReadMetaDataFromFile(bom, importMetadataPath);
                }
            }

            if (bom.Metadata is null)
            {
                bom.Metadata = new Metadata
                {
                    Component = topLevelComponent
                };
            }
            else if (bom.Metadata.Component is null)
            {
                bom.Metadata.Component = topLevelComponent;
            }
            else
            {
                if (string.IsNullOrEmpty(bom.Metadata.Component.Name))
                {
                    bom.Metadata.Component.Name = topLevelComponent.Name;
                }
                if (string.IsNullOrEmpty(bom.Metadata.Component.Version))
                {
                    bom.Metadata.Component.Version = topLevelComponent.Version;
                }
                if (bom.Metadata.Component.Type == Component.Classification.Null)
                {
                    bom.Metadata.Component.Type = Component.Classification.Application;
                }
            }

            if (string.IsNullOrEmpty(bom.Metadata.Component.BomRef))
            {
                bom.Metadata.Component.BomRef = $"{bom.Metadata.Component.Name}@{bom.Metadata.Component.Version}";
            }

            AddMetadataTool(bom);

            if (!(noSerialNumber || noSerialNumberDeprecated)) bom.SerialNumber = "urn:uuid:" + System.Guid.NewGuid().ToString();
            bom.Components = new List<Component>(components);
            bom.Components.Sort((x, y) =>
            {
                if (x.Name == y.Name)
                    return string.Compare(x.Version, y.Version, StringComparison.InvariantCultureIgnoreCase);
                return string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
            });
            bom.Dependencies = dependencies;
            directDependencies.Ref = bom.Metadata.Component.BomRef;
            bom.Dependencies.Add(directDependencies);
            bom.Dependencies.Sort((x, y) => string.Compare(x.Ref, y.Ref, StringComparison.InvariantCultureIgnoreCase));

            var bomContents = BomService.CreateDocument(bom, json);

            // check if the output directory exists and create it if needed
            var bomPath = Program.fileSystem.Path.GetFullPath(outputDirectory);
            if (!Program.fileSystem.Directory.Exists(bomPath))
                Program.fileSystem.Directory.CreateDirectory(bomPath);

            // write the BOM to disk
            var bomFilename = Program.fileSystem.Path.Combine(bomPath, json ? "bom.json" : "bom.xml");
            Console.WriteLine("Writing to: " + bomFilename);
            Program.fileSystem.File.WriteAllText(bomFilename, bomContents);

            return 0;
        }

        public static Bom ReadMetaDataFromFile(Bom bom, string templatePath)
        {
            try
            {
                return Xml.Serializer.Deserialize(File.ReadAllText(templatePath));
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Could not read Metadata file.");
                Console.WriteLine(ex.Message);
            }
            return bom;
        }
        public static void AddMetadataTool(Bom bom)
        {
            string toolname = "CycloneDX module for .NET";

            if (bom.Metadata == null) {
                bom.Metadata = new Metadata();
            }
            if (bom.Metadata.Tools == null)
            {
                bom.Metadata.Tools = new List<Tool>();
            }
            var index = bom.Metadata.Tools.FindIndex(p => p.Name == toolname);
            if (index == -1)
            {
                bom.Metadata.Tools.Add(new Tool
                {
                    Name = toolname,
                    Vendor = "CycloneDX",
                    Version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                }
                );
            }
            else
            {
                bom.Metadata.Tools[index].Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }

        }

    }
}
