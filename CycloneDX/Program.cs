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
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX
{
    public static class Program
    {

        internal static IFileSystem fileSystem = new FileSystem();
        internal static readonly IDotnetCommandService dotnetCommandService = new DotnetCommandService();
        internal static readonly IProjectAssetsFileService projectAssetsFileService = new ProjectAssetsFileService(fileSystem, dotnetCommandService, () => new AssetFileReader());
        internal static readonly IDotnetUtilsService dotnetUtilsService = new DotnetUtilsService(fileSystem, dotnetCommandService);
        internal static readonly IPackagesFileService packagesFileService = new PackagesFileService(fileSystem);
        internal static IProjectFileService projectFileService = new ProjectFileService(fileSystem, dotnetUtilsService, packagesFileService, projectAssetsFileService);
        internal static ISolutionFileService solutionFileService = new SolutionFileService(fileSystem, projectFileService);

        public static Task<int> Main(string[] args)
        {

            var SolutionOrProjectFile = new Argument<string>("path", description: "The path to a .sln, .csproj, .fsproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files.");
            var framework = new Option<string>(new[] { "--framework", "-tfm" }, "The target framework to use. If not defined, all will be aggregated.");
            var runtime = new Option<string>(new[] { "--runtime", "-rt" }, "The runtime to use. If not defined, all will be aggregated.");
            var outputDirectory = new Option<string>(new[] { "--output", "-o" }, description: "The directory to write the BOM") { IsRequired = true };
            var outputFilename = new Option<string>(new[] { "--filename", "-f" }, "Optionally provide a filename for the BOM (default: bom.xml or bom.json");
            var json = new Option<bool>(new[] { "--json", "-j" }, "Produce a JSON BOM instead of XML");
            var excludeDev = new Option<bool>(new[] { "--exclude-dev", "-d" }, "Exclude development dependencies from the BOM (see https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference)");
            var excludetestprojects = new Option<bool>(new[] { "--exclude-test-projects", "-t" }, "Exclude test projects from the BOM");
            var baseUrl = new Option<string>(new[] { "--url", "-u" }, "Alternative NuGet repository URL to https://<yoururl>/nuget/<yourrepository>/v3/index.json");
            var baseUrlUS = new Option<string>(new[] { "--baseUrlUsername", "-us" }, "Alternative NuGet repository username");
            var baseUrlUSP = new Option<string>(new[] { "--baseUrlUserPassword", "-usp" }, "Alternative NuGet repository username password/apikey");
            var isPasswordClearText = new Option<bool>(new[] { "--isBaseUrlPasswordClearText", "-uspct" }, "Alternative NuGet repository password is cleartext");
            var scanProjectReferences = new Option<bool>(new[] { "--recursive", "-r" }, "To be used with a single project file, it will recursively scan project references of the supplied project file");
            var noSerialNumber = new Option<bool>(new[] { "--no-serial-number", "-ns" }, "Optionally omit the serial number from the resulting BOM");
            var githubUsername = new Option<string>(new[] { "--github-username", "-gu" }, "Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token");
            var githubT = new Option<string>(new[] { "--github-token", "-gt" }, "Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username");
            var githubBT = new Option<string>(new[] { "--github-bearer-token", "-gbt" }, "Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions");
            var disableGithubLicenses = new Option<bool>(new[] { "--disable-github-licenses", "-dgl" }, "Optionally disable GitHub license resolution");
            var disablePackageRestore = new Option<bool>(new[] { "--disable-package-restore", "-dpr" }, "Optionally disable package restore");
            var disableHashComputation = new Option<bool>(new[] { "--disable-hash-computation", "-dhc" }, "Optionally disable hash computation for packages");
            var dotnetCommandTimeout = new Option<int>(new[] { "--dotnet-command-timeout", "-dct" }, description: "dotnet command timeout in milliseconds (primarily used for long dotnet restore operations)", getDefaultValue: () => 300000);
            var baseIntermediateOutputPath = new Option<string>(new[] { "--base-intermediate-output-path", "-biop" }, "Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated.");
            var importMetadataPath = new Option<string>(new[] { "--import-metadata-path", "-imp" }, "Optionally provide a metadata template which has project specific details.");
            var setName = new Option<string>(new[] { "--set-name", "-sn" }, "Override the autogenerated BOM metadata component name.");
            var setVersion = new Option<string>(new[] { "--set-version", "-sv" }, "Override the default BOM metadata component version (defaults to 0.0.0).");
            var setType = new Option<Component.Classification>(new[] { "--set-type", "-st" }, getDefaultValue: () => Component.Classification.Application, "Override the default BOM metadata component type (defaults to application).");

            RootCommand rootCommand = new RootCommand
            {
                SolutionOrProjectFile,
                framework,
                runtime,
                outputDirectory,
                outputFilename,
                json,
                excludeDev,
                excludetestprojects,
                baseUrl,
                baseUrlUS,
                baseUrlUSP,
                isPasswordClearText,
                scanProjectReferences,
                noSerialNumber,
                githubUsername,
                githubT,
                githubBT,
                disableGithubLicenses,
                disablePackageRestore,
                disableHashComputation,
                dotnetCommandTimeout,
                baseIntermediateOutputPath,
                importMetadataPath,
                setName,
                setVersion,
                setType
            };
            rootCommand.Description = "A .NET Core global tool which creates CycloneDX Software Bill-of-Materials (SBOM) from .NET projects.";
            rootCommand.SetHandler(async (context) =>
            {
                string SolutionOrProjectFileValue = context.ParseResult.GetValueForArgument(SolutionOrProjectFile);
                string runtimeValue = context.ParseResult.GetValueForOption(runtime);
                string frameworkValue = context.ParseResult.GetValueForOption(framework);
                string outputDirectoryValue = context.ParseResult.GetValueForOption(outputDirectory);
                string outputFilenameValue = context.ParseResult.GetValueForOption(outputFilename);
                bool jsonValue = context.ParseResult.GetValueForOption(json);
                bool excludeDevValue = context.ParseResult.GetValueForOption(excludeDev);
                bool excludeTestProjectsValue = context.ParseResult.GetValueForOption(excludetestprojects);
                string baseUrlValue = context.ParseResult.GetValueForOption(baseUrl);
                string baseUrlUserNameValue = context.ParseResult.GetValueForOption(baseUrlUS);
                string baseUrlUSPValue = context.ParseResult.GetValueForOption(baseUrlUSP);
                bool isPasswordClearTextValue = context.ParseResult.GetValueForOption(isPasswordClearText);
                bool scanProjectReferencesValue = context.ParseResult.GetValueForOption(scanProjectReferences);
                bool noSerialNumberValue = context.ParseResult.GetValueForOption(noSerialNumber);
                string githubUsernameValue = context.ParseResult.GetValueForOption(githubUsername);
                string githubTValue = context.ParseResult.GetValueForOption(githubT);
                string githubBTValue = context.ParseResult.GetValueForOption(githubBT);
                bool disableGithubLicensesValue = context.ParseResult.GetValueForOption(disableGithubLicenses);
                bool disablePackageRestoreValue = context.ParseResult.GetValueForOption(disablePackageRestore);
                bool disableHashComputationValue = context.ParseResult.GetValueForOption(disableHashComputation);
                int dotnetCommandTimeoutValue = context.ParseResult.GetValueForOption(dotnetCommandTimeout);
                string baseIntermediateOutputPathValue = context.ParseResult.GetValueForOption(baseIntermediateOutputPath);
                string importMetadataPathValue = context.ParseResult.GetValueForOption(importMetadataPath);
                string setNameValue = context.ParseResult.GetValueForOption(setName);
                string setVersionValue = context.ParseResult.GetValueForOption(setVersion);
                Component.Classification setTypeValue = context.ParseResult.GetValueForOption(setType);

                var taskStatus = await HandleCommandAsync(
                    SolutionOrProjectFileValue,
                    runtimeValue,
                    frameworkValue,
                    outputDirectoryValue,
                    outputFilenameValue,
                    jsonValue,
                    excludeDevValue,
                    excludeTestProjectsValue,
                    baseUrlValue,
                    baseUrlUserNameValue,
                    baseUrlUSPValue,
                    isPasswordClearTextValue,
                    scanProjectReferencesValue,
                    noSerialNumberValue,
                    githubUsernameValue,
                    githubTValue,
                    githubBTValue,
                    disableGithubLicensesValue,
                    disablePackageRestoreValue,
                    disableHashComputationValue,
                    dotnetCommandTimeoutValue,
                    baseIntermediateOutputPathValue,
                    importMetadataPathValue,
                    setNameValue,
                    setVersionValue,
                    setTypeValue);
                context.ExitCode = taskStatus;

            });
            return Task.FromResult(rootCommand.Invoke(args));
        }

        private static async Task<int> HandleCommandAsync(string SolutionOrProjectFile,
                                         string framework,
                                         string runtime,
                                         string outputDirectory,
                                         string outputFilename,
                                         bool json,
                                         bool excludeDev,
                                         bool excludetestprojects,
                                         string baseUrl,
                                         string baseUrlUserName,
                                         string baseUrlUP,
                                         bool isPasswordClearText,
                                         bool scanProjectReferences,
                                         bool noSerialNumber,
                                         string githubUsername,
                                         string githubT,
                                         string githubBT,
                                         bool disableGithubLicenses,
                                         bool disablePackageRestore,
                                         bool disableHashComputation,
                                         int dotnetCommandTimeout,
                                         string baseIntermediateOutputPath,
                                         string importMetadataPath,
                                         string setName,
                                         string setVersion,
                                         Component.Classification setType
            )
        {

            Console.WriteLine();

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
            foreach (var cachePath in packageCachePathsResult.Result)
            {
                Console.WriteLine($"    {cachePath}");
            }

            // instantiate services

            var fileDiscoveryService = new FileDiscoveryService(Program.fileSystem);
            GithubService githubService = null;
            if (!(disableGithubLicenses))
            {
                // GitHubService requires its own HttpClient as it adds a default authorization header
                HttpClient httpClient = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = false
                });

                if (!string.IsNullOrEmpty(githubBT))
                {
                    githubService = new GithubService(httpClient, githubBT);
                }
                else if (!string.IsNullOrEmpty(githubUsername))
                {
                    githubService = new GithubService(httpClient, githubUsername, githubT);
                }
                else
                {
                    githubService = new GithubService(new HttpClient());
                }
            }
            var nugetLogger = new NuGet.Common.NullLogger();
            var nugetInput =
                NugetInputFactory.Create(baseUrl, baseUrlUserName, baseUrlUP, isPasswordClearText);
            var nugetService = new NugetV3Service(nugetInput, fileSystem, packageCachePathsResult.Result, githubService, nugetLogger, disableHashComputation);

            var packages = new HashSet<NugetPackage>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = Program.fileSystem.Path.GetFullPath(SolutionOrProjectFile);

            var topLevelComponent = new Component
            {
                // name is set below
                Version = string.IsNullOrEmpty(setVersion) ? "0.0.0" : setVersion,
                Type = setType == Component.Classification.Null ? Component.Classification.Application : setType

            };

            try
            {
                if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    packages = await solutionFileService.GetSolutionNugetPackages(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
                {
                    packages = await projectFileService.RecursivelyGetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile))
                {
                    packages = await projectFileService.GetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
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
                    Console.Error.WriteLine($"Only .sln, .csproj, .fsproj, .vbproj, and packages.config files are supported");
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

            // get all the components and dependency graph from the NuGet packages
            var components = new HashSet<Component>();
            var dependencies = new List<Dependency>();
            var directDependencies = new Dependency { Dependencies = new List<Dependency>() };
            var transitiveDependencies = new HashSet<string>();
            var packageToComponent = new Dictionary<NugetPackage, Component>();
            try
            {
                var bomRefLookup = new Dictionary<(string, string), string>();
                foreach (var package in packages)
                {
                    var component = await nugetService.GetComponentAsync(package).ConfigureAwait(false);
                    if (component != null)
                    {
                        if (component.Scope != Component.ComponentScope.Excluded || !excludeDev)
                        {
                            components.Add(component);
                        }
                        packageToComponent[package] = component;
                        bomRefLookup[(component.Name.ToLower(CultureInfo.InvariantCulture), (component.Version.ToLower(CultureInfo.InvariantCulture)))] = component.BomRef;
                    }
                }
                // now that we have all the bom ref lookups we need to enumerate all the dependencies
                foreach (var package in packages)
                {
                    var packageDependencies = new Dependency
                    {
                        Ref = bomRefLookup[(package.Name.ToLower(CultureInfo.InvariantCulture), package.Version.ToLower(CultureInfo.InvariantCulture))],
                        Dependencies = new List<Dependency>()
                    };
                    if (package.Dependencies != null && package.Dependencies.Any())
                    {
                        foreach (var dep in package.Dependencies)
                        {
                            var lookupKey = (dep.Key.ToLower(CultureInfo.InvariantCulture), dep.Value.ToLower(CultureInfo.InvariantCulture));
                            if (!bomRefLookup.ContainsKey(lookupKey))
                            {
                                var packageNameMatch = bomRefLookup.Where(x => x.Key.Item1 == dep.Key.ToLower(CultureInfo.InvariantCulture)).ToList();
                                if (packageNameMatch.Count == 1)
                                {
                                    lookupKey = packageNameMatch.First().Key;
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unable to locate valid bom ref for {dep.Key} {dep.Value} referenced by (Name: {package.Name} Version: {package.Version})");
                                    return (int)ExitCode.UnableToLocateDependencyBomRef;
                                }
                            }

                            var bomRef = bomRefLookup[lookupKey];
                            transitiveDependencies.Add(bomRef);
                            packageDependencies.Dependencies.Add(new Dependency
                            {
                                Ref = bomRef
                            });
                        }
                    }
                    dependencies.Add(packageDependencies);
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

            // Automatically generate a timestamp if none is provided with the metadata
            if (bom.Metadata.Timestamp == null)
            {
                bom.Metadata.Timestamp = DateTime.UtcNow;
            }

            AddMetadataTool(bom);

            if (!(noSerialNumber))
            {
                bom.SerialNumber = "urn:uuid:" + System.Guid.NewGuid().ToString();
            }
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
            {
                Program.fileSystem.Directory.CreateDirectory(bomPath);
            }

            // write the BOM to disk
            var bomFilename = outputFilename;
            if (string.IsNullOrEmpty(bomFilename))
            {
                bomFilename = json ? "bom.json" : "bom.xml";
            }
            var bomFilePath = Program.fileSystem.Path.Combine(bomPath, bomFilename);
            Console.WriteLine("Writing to: " + bomFilePath);
            Program.fileSystem.File.WriteAllText(bomFilePath, bomContents);

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

            if (bom.Metadata == null)
            {
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
