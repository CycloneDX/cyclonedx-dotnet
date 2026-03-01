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
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using static CycloneDX.Models.Component;
using Json.Schema;

namespace CycloneDX
{
    public class Runner
    {
        public Bom LastGeneratedBom { get; set; }
        readonly IFileSystem fileSystem;
        readonly IDotnetCommandService dotnetCommandService;
        readonly IDotnetUtilsService dotnetUtilsService;
        readonly IPackagesFileService packagesFileService;
        readonly IProjectFileService projectFileService;
        readonly ISolutionFileService solutionFileService;
        readonly INugetServiceFactory nugetServiceFactory;

        public Runner(IFileSystem fileSystem,
                      IDotnetCommandService dotnetCommandService,
                      IProjectAssetsFileService projectAssetsFileService,
                      IDotnetUtilsService dotnetUtilsService,
                      IPackagesFileService packagesFileService,
                      IProjectFileService projectFileService,
                      ISolutionFileService solutionFileService,
                      INugetServiceFactory nugetServiceFactory)
        {
            this.fileSystem = fileSystem ?? new FileSystem();
            this.dotnetCommandService = dotnetCommandService ?? new DotnetCommandService();
            projectAssetsFileService ??= new ProjectAssetsFileService(this.fileSystem, () => new AssetFileReader());
            this.dotnetUtilsService = dotnetUtilsService ?? new DotnetUtilsService(this.fileSystem, this.dotnetCommandService);
            this.packagesFileService = packagesFileService ?? new PackagesFileService(this.fileSystem);
            this.projectFileService = projectFileService ?? new ProjectFileService(this.fileSystem, this.dotnetUtilsService, this.packagesFileService, projectAssetsFileService);
            this.solutionFileService = solutionFileService ?? new SolutionFileService(this.fileSystem, this.projectFileService);
            this.nugetServiceFactory = nugetServiceFactory ?? new NugetV3ServiceFactory();
        }
        public Runner() : this(null, null, null, null, null, null, null, null) { }

        public async Task<int> HandleCommandAsync(RunOptions options)
        {
            options.outputDirectory ??= fileSystem.Directory.GetCurrentDirectory();
            string outputDirectory = options.outputDirectory;
            string SolutionOrProjectFile = options.SolutionOrProjectFile;
            string framework = options.framework;
            string runtime = options.runtime;
            string outputFilename = options.outputFilename;
            bool excludeDev = options.excludeDev;
            bool excludetestprojects = options.excludeTestProjects;
            bool scanProjectReferences = options.scanProjectReferences;
            bool noSerialNumber = options.noSerialNumber;
            string githubUsername = options.githubUsername;
            string githubT = options.githubT;
            string githubBT = options.githubBT;
            bool disablePackageRestore = options.disablePackageRestore;
            int dotnetCommandTimeout = options.dotnetCommandTimeout;
            string baseIntermediateOutputPath = options.baseIntermediateOutputPath;
            string importMetadataPath = options.importMetadataPath;
            string setName = options.setName;
            string setVersion = options.setVersion;
            Classification setType = options.setType;
            bool setNugetPurl = options.setNugetPurl;
            Models.OutputFileFormat outputFormat = options.outputFormat;
            SpecificationVersion specVersion = options.specVersion ?? SpecificationVersionHelpers.CurrentVersion;


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
            GithubService githubService = null;
            if (options.enableGithubLicenses)
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

            var nugetService = nugetServiceFactory.Create(options, fileSystem, githubService, packageCachePathsResult.Result);

            var packages = new HashSet<DotnetDependency>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = this.fileSystem.Path.GetFullPath(SolutionOrProjectFile);
            await Console.Out.WriteLineAsync($"Scanning at {fullSolutionOrProjectFilePath}");

            var topLevelComponent = new Component
            {
                // name is set below
                Version = string.IsNullOrEmpty(setVersion) ? "0.0.0" : setVersion,
                Type = setType == Component.Classification.Null ? Component.Classification.Application : setType

            };


            if (options.includeProjectReferences
                &&
                    (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                    SolutionOrProjectFile.ToLowerInvariant().EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                    SolutionOrProjectFile.ToLowerInvariant().EndsWith(".slnf", StringComparison.OrdinalIgnoreCase) ||
                    fileSystem.Directory.Exists(fullSolutionOrProjectFilePath)
                    ||
                    this.fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Option -ipr can only be used with a project file");
                return (int)ExitCode.InvalidOptions;
            }


            try
            {
                if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || 
                    SolutionOrProjectFile.ToLowerInvariant().EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||                
                    SolutionOrProjectFile.ToLowerInvariant().EndsWith(".slnf", StringComparison.OrdinalIgnoreCase))
                {
                    if (!fileSystem.File.Exists(SolutionOrProjectFile))
                    {
                        Console.Error.WriteLine($"No file found at path {SolutionOrProjectFile}");
                        return (int)ExitCode.InvalidOptions;
                    }
                    packages = await solutionFileService.GetSolutionDotnetDependencys(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
                {
                    if (!fileSystem.File.Exists(SolutionOrProjectFile))
                    {
                        Console.Error.WriteLine($"No file found at path {SolutionOrProjectFile}");
                        return (int)ExitCode.InvalidOptions;
                    }
                     packages = await projectFileService.RecursivelyGetProjectDotnetDependencysAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                    if (string.IsNullOrEmpty(setVersion))
                    {
                        var (_, projVersion) = projectFileService.GetAssemblyNameAndVersion(fullSolutionOrProjectFilePath);
                        if (!string.IsNullOrEmpty(projVersion) && projVersion != "undefined")
                            topLevelComponent.Version = projVersion;
                    }
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile))
                {
                    if (!fileSystem.File.Exists(SolutionOrProjectFile))
                    {
                        Console.Error.WriteLine($"No file found at path {SolutionOrProjectFile}");
                        return (int)ExitCode.InvalidOptions;
                    }
                    packages = await projectFileService.GetProjectDotnetDependencysAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                    if (string.IsNullOrEmpty(setVersion))
                    {
                        var (_, projVersion) = projectFileService.GetAssemblyNameAndVersion(fullSolutionOrProjectFilePath);
                        if (!string.IsNullOrEmpty(projVersion) && projVersion != "undefined")
                            topLevelComponent.Version = projVersion;
                    }
                }
                else if (fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                {
                    if (!fileSystem.File.Exists(SolutionOrProjectFile))
                    {
                        Console.Error.WriteLine($"No file found at path {SolutionOrProjectFile}");
                        return (int)ExitCode.InvalidOptions;
                    }
                    packages = await packagesFileService.GetDotnetDependencysAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetDirectoryName(fullSolutionOrProjectFilePath);
                }
                else if (fileSystem.Directory.Exists(fullSolutionOrProjectFilePath))
                {
                    packages = await packagesFileService.RecursivelyGetDotnetDependencysAsync(fullSolutionOrProjectFilePath).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetDirectoryName(fullSolutionOrProjectFilePath);
                }
                else
                {
                    Console.Error.WriteLine($"Only .sln, .slnx, .slnf, .csproj, .fsproj, .vbproj, .xsproj, and packages.config files are supported");
                    return (int)ExitCode.InvalidOptions;
                }
            }
            catch (DotnetRestoreException)
            {
                return (int)ExitCode.DotnetRestoreFailed;
            }

            // Apply package exclude filter
            // The exclude filter may be used to exclude any packages, which are resolved by NuGet, but do not exist
            // in the final binary output. For example, an application targets .NET 8, but has a dependency to a library,
            // which only supports .NET Standard 1.6. Without filter, the libraries of .NET Standard 1.6 would be in the
            // resulting SBOM. But they are not used by application as they do not exist in the binary output folder.
            try
            {
                if (!string.IsNullOrEmpty(options.DependencyExcludeFilter))
                {
                    ExcludeFilterHelper.ExcludePackages(packages, options.DependencyExcludeFilter);
                    ExcludeFilterHelper.RemoveOrphanedPackages(packages);
                }
            }
            catch (ArgumentException e)
            {
                await Console.Error.WriteLineAsync(e.Message).ConfigureAwait(false);
                return (int)ExitCode.InvalidOptions;
            }

            // Remove transitive (via project references) dev-dependencies
            // Dev dependencies of referenced projects are typically not included in the assets file.
            // However, if the dev-dependency is transitive—meaning another dependency of that project depends on it—
            // the dev-dependency will be listed in the assets file under targets with a version range.
            // But a corresponding entry under libraries is missing.
            // This results in a state where there is a dependency on a package but no corresponding package.
            // To resolve this, we remove such dependencies.
            var allDependencies = packages.Where(p => p.Dependencies is not null).SelectMany(p => p.Dependencies!.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dependenciesWithoutPackages = allDependencies.Except(packages.Select(p => p.Name), StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var package in packages)
            {
                if (package.Dependencies is null)
                {
                    continue;
                }

                foreach (var dep in dependenciesWithoutPackages)
                {
                    if (package.Dependencies.Remove(dep))
                    {
                        await Console.Out.WriteLineAsync($"Removed transitive dependency {dep} from {package.Name}");
                    }
                }
            }
            Console.ResetColor();


            await Console.Out.WriteLineAsync($"Found {packages.Count()} packages");


            if (!string.IsNullOrEmpty(setName))
            {
                topLevelComponent.Name = setName;
            }


            if (excludeDev)
            {
                foreach (var item in packages.Where(p => p.IsDevDependency))
                {
                    item.Scope = ComponentScope.Excluded;
                }
                await Console.Out.WriteLineAsync($"{packages.Where(p => p.IsDevDependency).Count()} packages being excluded as DevDependencies");
            }




            // get all the components and dependency graph from the NuGet packages
            var components = new HashSet<Component>();
            var dependencies = new List<Dependency>();
            var directDependencies = new Dependency { Dependencies = new List<Dependency>() };
            var transitiveDependencies = new HashSet<string>();
            var packageToComponent = new Dictionary<DotnetDependency, Component>();
            try
            {
                var bomRefLookup = new Dictionary<(string, string), string>();
                foreach (var package in packages)
                {
                    Component component = null;
                    if (package.DependencyType == DependencyType.Package)
                    {
                        component = await nugetService.GetComponentAsync(package).ConfigureAwait(false);
                    }
                    else if (package.DependencyType == DependencyType.Project)
                    {
                        component = projectFileService.GetComponent(package);
                    }

                    if (component != null)
                    {
                        if ((component.Scope != Component.ComponentScope.Excluded || !excludeDev)
                            &&
                            (options.includeProjectReferences || package.DependencyType == DependencyType.Package))
                        {
                            components.Add(component);
                        }
                        packageToComponent[package] = component;
                        bomRefLookup[(component.Name.ToLower(CultureInfo.InvariantCulture), (component.Version.ToLower(CultureInfo.InvariantCulture)))] = component.BomRef;
                    }
                }
                if (!options.includeProjectReferences)
                {
                    packages = RemoveProjectReferencesAndMakeTheirDependenciesDirect(packages);
                }

                // now that we have all the bom ref lookups we need to enumerate all the dependencies
                foreach (var package in packages.Where(p => !excludeDev || packageToComponent[p].Scope != Component.ComponentScope.Excluded))
                {
                    var packageDependencies = new Dependency
                    {
                        Ref = bomRefLookup[(package.Name.ToLower(CultureInfo.InvariantCulture), package.Version.ToLower(CultureInfo.InvariantCulture))],
                        Dependencies = new List<Dependency>()
                    };

                    foreach (var dep in package.Dependencies ?? [])
                    {
                        var lookupKey = (dep.Key.ToLower(CultureInfo.InvariantCulture), dep.Value?.ToLower(CultureInfo.InvariantCulture));
                        if (!bomRefLookup.ContainsKey(lookupKey))
                        {
                            var packageNameMatch = bomRefLookup.Where(x => x.Key.Item1 == dep.Key.ToLower(CultureInfo.InvariantCulture)).ToList();
                            if (packageNameMatch.Count == 1)
                            {
                                lookupKey = packageNameMatch.First().Key;
                            }
                            else
                            {
                                Console.Error.WriteLine($"Unable to locate valid bom ref for {dep.Key} {dep.Value}");
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
                SpecVersion = specVersion
            };


            if (!string.IsNullOrEmpty(importMetadataPath))
            {
                if (!fileSystem.File.Exists(importMetadataPath))
                {
                    Console.Error.WriteLine($"Metadata template '{importMetadataPath}' does not exist.");
                    return (int)ExitCode.InvalidOptions;
                }
                else
                {
                    bom = ReadMetaDataFromFile(bom, importMetadataPath, fileSystem);
                }
            }
            SetMetadataComponentIfNecessary(bom, topLevelComponent, setNugetPurl, setName, setVersion, setType);
            Runner.AddMetadataTool(bom);

            if (!(noSerialNumber))
            {
                bom.SerialNumber = "urn:uuid:" + System.Guid.NewGuid().ToString();
            }

            bom.Components = new List<Component>(components);
            bom.Components.Sort((x, y) =>
            {
                if (x.Name == y.Name)
                {
                    return string.Compare(x.Version, y.Version, StringComparison.InvariantCultureIgnoreCase);
                }
                return string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
            });
            bom.Dependencies = dependencies;
            directDependencies.Ref = bom.Metadata.Component.BomRef;
            bom.Dependencies.Add(directDependencies);
            bom.Dependencies.Sort((x, y) => string.Compare(x.Ref, y.Ref, StringComparison.InvariantCultureIgnoreCase));

            LastGeneratedBom = bom;

            var (format, filename) = DetermineOutputFileFormatAndFilename(outputFormat, outputFilename);
            var bomContents = BomService.CreateDocument(bom, format);

            // check if the output directory exists and create it if needed
            var bomPath = this.fileSystem.Path.GetFullPath(outputDirectory);
            if (!this.fileSystem.Directory.Exists(bomPath))
            {
                this.fileSystem.Directory.CreateDirectory(bomPath);
            }

            // write the BOM to disk
            var bomFilename = filename;

            var bomFilePath = this.fileSystem.Path.Combine(bomPath, bomFilename);
            Console.WriteLine("Writing to: " + bomFilePath);
            this.fileSystem.File.WriteAllText(bomFilePath, bomContents);

            return 0;
        }

        (OutputFileFormat format, string outputFileName) DetermineOutputFileFormatAndFilename(
            OutputFileFormat selectedFormat,
            string userProvidedFilename
        )
        {
            OutputFileFormat resolvedFormat = selectedFormat;
            string filename = userProvidedFilename ?? string.Empty;

            // Auto deduction based on filename
            if (resolvedFormat == OutputFileFormat.Auto)
            {
                if (!string.IsNullOrEmpty(filename))
                {
                    if (filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        resolvedFormat = OutputFileFormat.Json;
                    else if (filename.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        resolvedFormat = OutputFileFormat.Xml;
                    else
                        resolvedFormat = OutputFileFormat.Xml; // Default fallback
                }
                else
                {
                    resolvedFormat = OutputFileFormat.Xml;
                }
            }

            // Filename fallback
            if (string.IsNullOrEmpty(filename))
            {
                filename = resolvedFormat switch
                {
                    OutputFileFormat.Json or OutputFileFormat.UnsafeJson => "bom.json",
                    _ => "bom.xml"
                };
            }

            return (resolvedFormat, filename);
        }




        private static HashSet<DotnetDependency> RemoveProjectReferencesAndMakeTheirDependenciesDirect(HashSet<DotnetDependency> packages)
        {
            var projectReferences = packages.Where(p => p.DependencyType == DependencyType.Project);
            // Change all packages that are refered to by a project to direct dependency
            var dependenciesOfProjects = projectReferences.SelectMany(p => p.Dependencies);
            var newDirectDependencies = packages.Join(dependenciesOfProjects, p => p.Name + '@' + p.Version, d => d.Key + '@' + d.Value, (p, _) => p);
            newDirectDependencies.ToList().ForEach(p => p.IsDirectReference = true);
            //remove all dependencies of packages to project references (https://github.com/CycloneDX/cyclonedx-dotnet/issues/785)
            var projectReferencesNames = projectReferences.Select(p => p.Name);
            foreach (var package in packages)
            {
                foreach (var refName in projectReferencesNames)
                {
                    package.Dependencies?.Remove(refName);
                }
            }

            //remove project references from list
            packages = packages.Where(p => p.DependencyType != DependencyType.Project).ToHashSet();
            return packages;
        }

        private static void SetMetadataComponentIfNecessary(Bom bom, Component topLevelComponent, bool setNugetPurl,
            string setName = null, string setVersion = null, Classification setType = Classification.Null)
        {
            if (bom.Metadata is null)
            {
                bom.Metadata = new Metadata { Component = topLevelComponent };
            }
            else if (bom.Metadata.Component is null)
            {
                bom.Metadata.Component = topLevelComponent;
            }
            else
            {
                // If the user explicitly provided a name/version/type override, apply it regardless
                // of what was imported from metadata; otherwise only fill in missing values.
                if (!string.IsNullOrEmpty(setName))
                {
                    bom.Metadata.Component.Name = setName;
                }
                else if (string.IsNullOrEmpty(bom.Metadata.Component.Name))
                {
                    bom.Metadata.Component.Name = topLevelComponent.Name;
                }

                if (!string.IsNullOrEmpty(setVersion))
                {
                    bom.Metadata.Component.Version = setVersion;
                }
                else if (string.IsNullOrEmpty(bom.Metadata.Component.Version))
                {
                    bom.Metadata.Component.Version = topLevelComponent.Version;
                }

                if (setType != Classification.Null)
                {
                    bom.Metadata.Component.Type = setType;
                }
                else if (bom.Metadata.Component.Type == Component.Classification.Null)
                {
                    bom.Metadata.Component.Type = Component.Classification.Application;
                }
            }

            if (setNugetPurl)
            {
                if (string.IsNullOrEmpty(bom.Metadata.Component.Purl))
                {
                    bom.Metadata.Component.Purl = Utils.GeneratePackageUrl(bom.Metadata.Component.Name, bom.Metadata.Component.Version);
                }
                if (string.IsNullOrEmpty(bom.Metadata.Component.BomRef))
                {
                    bom.Metadata.Component.BomRef = bom.Metadata.Component.Purl;
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

        }

        internal static Bom ReadMetaDataFromFile(Bom bom, string templatePath, IFileSystem fileSystem)
        {
            try
            {
                var templateBom = Xml.Serializer.Deserialize(fileSystem.File.ReadAllText(templatePath));
                bom.Metadata = templateBom.Metadata;
                return bom;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Could not read Metadata file.");
                Console.WriteLine(ex.Message);
            }
            return bom;
        }

        internal static void AddMetadataTool(Bom bom)
        {
            string toolname = "CycloneDX module for .NET";

            bom.Metadata ??= new Metadata();
            bom.Metadata.Tools ??= new ToolChoices();
#pragma warning disable CS0618 // Type or member is obsolete
            bom.Metadata.Tools.Tools ??= new List<Tool>();
#pragma warning restore CS0618 // Type or member is obsolete

            var index = bom.Metadata.Tools.Tools.FindIndex(p => p.Name == toolname);
            if (index == -1)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                bom.Metadata.Tools.Tools.Add(new Tool
                {
                    Name = toolname,
                    Vendor = "CycloneDX",
                    Version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                }
                );
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else
            {
                bom.Metadata.Tools.Tools[index].Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
    }
}
