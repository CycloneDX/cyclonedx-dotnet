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
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using CycloneDX.Models;
using CycloneDX.Core.Models;
using CycloneDX.Services;

namespace CycloneDX {
    [Command(Name = "dotnet cyclonedx", FullName = "A .NET Core global tool which creates CycloneDX Software Bill-of-Materials (SBOM) from .NET projects.")]
    class Program {
        [Argument(0, Name = "path", Description = "The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files")]
        public string SolutionOrProjectFile { get; set; }

        [Option(Description = "The directory to write the BOM", ShortName = "o", LongName = "out")]
        string outputDirectory { get; }

        [Option(Description = "Produce a JSON BOM instead of XML", ShortName = "j", LongName = "json")]
        bool json { get; }

        [Option(Description = "Exclude development dependencies from the BOM", ShortName = "d", LongName = "exclude-dev")]
        bool excludeDev { get; }

        [Option(Description = "Exclude test projects from the BOM", ShortName = "t", LongName = "exclude-test-projects")]
        bool excludetestprojects { get; }

        [Option(Description = "Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required)", ShortName = "u", LongName = "url")]
        string baseUrl { get; set; }

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

        [Option(Description = "dotnet command timeout in milliseconds (primarily used for long dotnet restore operations)", ShortName = "dct", LongName = "dotnet-command-timeout")]
        int dotnetCommandTimeout { get; set; } = 300000;

        [Option(Description = "Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated.", ShortName = "biop", LongName = "base-intermediate-output-path")]
        public string baseIntermediateOutputPath { get; }

    static internal IFileSystem fileSystem = new FileSystem();
        static internal HttpClient httpClient = new HttpClient();
        static internal IProjectAssetsFileService projectAssetsFileService = new ProjectAssetsFileService(fileSystem);
        static internal IDotnetCommandService dotnetCommandService = new DotnetCommandService();
        static internal IDotnetUtilsService dotnetUtilsService = new DotnetUtilsService(fileSystem, dotnetCommandService);
        static internal IPackagesFileService packagesFileService = new PackagesFileService(fileSystem);
        static internal IProjectFileService projectFileService = new ProjectFileService(fileSystem, dotnetUtilsService, packagesFileService, projectAssetsFileService);
        static internal ISolutionFileService solutionFileService = new SolutionFileService(fileSystem, projectFileService);
        
        public static async Task<int> Main(string[] args)
            => await CommandLineApplication.ExecuteAsync<Program>(args).ConfigureAwait(false);

        async Task<int> OnExecuteAsync() {
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
            var nugetService = new NugetService(
                Program.fileSystem,
                packageCachePathsResult.Result,
                githubService,
                Program.httpClient,
                baseUrl);

            var packages = new HashSet<NugetPackage>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = Program.fileSystem.Path.GetFullPath(SolutionOrProjectFile);
            var attr = Program.fileSystem.File.GetAttributes(fullSolutionOrProjectFilePath);

            try
            {
                if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    packages = await solutionFileService.GetSolutionNugetPackages(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                }
                else if (Core.Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
                {
                    packages = await projectFileService.RecursivelyGetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                }
                else if (Core.Utils.IsSupportedProjectType(SolutionOrProjectFile))
                {
                    packages = await projectFileService.GetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects).ConfigureAwait(false);
                }
                else if (Program.fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                {
                    packages = await packagesFileService.GetNugetPackagesAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
                } 
                else if (attr.HasFlag(FileAttributes.Directory))
                {
                    packages = await packagesFileService.RecursivelyGetNugetPackagesAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
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
            
            // get all the components from the NuGet packages
            var components = new HashSet<Component>();
            try
            {
                foreach (var package in packages)
                {
                    var component = await nugetService.GetComponentAsync(package).ConfigureAwait(false);
                    if (component != null
                        && (component.Scope != "excluded" || !excludeDev)
                    )
                    {
                        components.Add(component);
                    }
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

            // create the BOM
            Console.WriteLine();
            Console.WriteLine("Creating CycloneDX BOM");
            var bom = new Bom();
            if (!(noSerialNumber || noSerialNumberDeprecated)) bom.SerialNumber = "urn:uuid:" + System.Guid.NewGuid().ToString();
            bom.Components = new List<Component>(components);
            bom.Components.Sort((x, y) => {
                if (x.Name == y.Name)
                    return string.Compare(x.Version, y.Version, StringComparison.InvariantCultureIgnoreCase);
                else
                    return string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
            });

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
    }
}
