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
using CycloneDX.Models;
using CycloneDX.Interfaces;
using CycloneDX.Services;
using static CycloneDX.Models.Component;
using NuGet.ProjectModel;

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
        public Runner(IFileSystem fileSystem,
                      IDotnetCommandService dotnetCommandService,
                      IProjectAssetsFileService projectAssetsFileService,
                      IDotnetUtilsService dotnetUtilsService,
                      IPackagesFileService packagesFileService,
                      IProjectFileService projectFileService,
                      ISolutionFileService solutionFileService)
        {
            this.fileSystem = fileSystem ?? new FileSystem();
            this.dotnetCommandService = dotnetCommandService ?? new DotnetCommandService();
            projectAssetsFileService ??= new ProjectAssetsFileService(this.fileSystem, this.dotnetCommandService, () => new AssetFileReader());
            this.dotnetUtilsService = dotnetUtilsService ?? new DotnetUtilsService(this.fileSystem, this.dotnetCommandService);
            this.packagesFileService = packagesFileService ?? new PackagesFileService(this.fileSystem);
            this.projectFileService = projectFileService ?? new ProjectFileService(this.fileSystem, this.dotnetUtilsService, this.packagesFileService, projectAssetsFileService);
            this.solutionFileService = solutionFileService ?? new SolutionFileService(this.fileSystem, this.projectFileService);
        }
        public Runner() : this(null, null, null, null, null, null, null) { }

        public async Task<int> HandleCommandAsync(RunOptions options)
        {
            string outputDirectory = options.outputDirectory;
            string SolutionOrProjectFile = options.SolutionOrProjectFile;
            string framework = options.framework;
            string runtime = options.runtime;
            string outputFilename = options.outputFilename;
            bool json = options.json;
            bool excludeDev = options.excludeDev;
            bool excludetestprojects = options.excludeTestProjects;
            string baseUrl = options.baseUrl;
            string baseUrlUserName = options.baseUrlUserName;
            string baseUrlUP = options.baseUrlUSP;
            bool isPasswordClearText = options.isPasswordClearText;
            bool scanProjectReferences = options.scanProjectReferences;
            bool noSerialNumber = options.noSerialNumber;
            string githubUsername = options.githubUsername;
            string githubT = options.githubT;
            string githubBT = options.githubBT;
            bool disableGithubLicenses = options.disableGithubLicenses;
            bool disablePackageRestore = options.disablePackageRestore;
            bool disableHashComputation = options.disableHashComputation;
            int dotnetCommandTimeout = options.dotnetCommandTimeout;
            string baseIntermediateOutputPath = options.baseIntermediateOutputPath;
            string importMetadataPath = options.importMetadataPath;
            string setName = options.setName;
            string setVersion = options.setVersion;
            Classification setType = options.setType;


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

            var packages = new HashSet<DotnetDependency>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = this.fileSystem.Path.GetFullPath(SolutionOrProjectFile);
            string basePath = fileSystem.Path.GetDirectoryName(fullSolutionOrProjectFilePath);

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
                    packages = await solutionFileService.GetSolutionDotnetDependencys(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
                {
                    packages = await projectFileService.RecursivelyGetProjectDotnetDependencysAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (Utils.IsSupportedProjectType(SolutionOrProjectFile))
                {
                    packages = await projectFileService.GetProjectDotnetDependencysAsync(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, excludeDev, framework, runtime).ConfigureAwait(false);
                    topLevelComponent.Name = fileSystem.Path.GetFileNameWithoutExtension(SolutionOrProjectFile);
                }
                else if (this.fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                {
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
                if(!options.includeProjectReferences)
                {
                    var projectReferences = packages.Where(p => p.DependencyType == DependencyType.Project);
                    // Change all packages that are refered to by a project to direct dependency
                    var dependenciesOfProjects = projectReferences.SelectMany(p => p.Dependencies);
                    var newDirectDependencies = packages.Join(dependenciesOfProjects, p => p.Name + '@' + p.Version, d => d.Key + '@' + d.Value, (p, d) => p);
                    newDirectDependencies.ToList().ForEach(p => p.IsDirectReference = true);
                    //remove all dependencies of packages to project references (https://github.com/CycloneDX/cyclonedx-dotnet/issues/758)
                    var projectReferencesNames = projectReferences.Select(p => p.Name);
                    foreach (var package in packages)
                    {
                        foreach (var refName in projectReferencesNames)
                        {
                            package.Dependencies.Remove(refName);
                        }
                    }

                    //remove project references from list
                    packages = packages.Where(p => p.DependencyType != DependencyType.Project).ToHashSet();
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
            SetMetadataComponentIfNecessary(bom, topLevelComponent);
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

            var bomContents = BomService.CreateDocument(bom, json);

            // check if the output directory exists and create it if needed
            var bomPath = this.fileSystem.Path.GetFullPath(outputDirectory);
            if (!this.fileSystem.Directory.Exists(bomPath))
            {
                this.fileSystem.Directory.CreateDirectory(bomPath);
            }

            // write the BOM to disk
            var bomFilename = outputFilename;
            if (string.IsNullOrEmpty(bomFilename))
            {
                bomFilename = json ? "bom.json" : "bom.xml";
            }
            var bomFilePath = this.fileSystem.Path.Combine(bomPath, bomFilename);
            Console.WriteLine("Writing to: " + bomFilePath);
            this.fileSystem.File.WriteAllText(bomFilePath, bomContents);

            return 0;
        }

  

        private static void SetMetadataComponentIfNecessary(Bom bom, Component topLevelComponent)
        {
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
        }

        internal static Bom ReadMetaDataFromFile(Bom bom, string templatePath)
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
