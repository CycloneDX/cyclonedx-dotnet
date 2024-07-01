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
            bool json = options.json;
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
            BomFormat bomFormat = options.BomFormat;
        
            Console.WriteLine();
        
            dotnetCommandService.TimeoutMilliseconds = dotnetCommandTimeout;
            projectFileService.DisablePackageRestore = disablePackageRestore;
        
            // Retrieve nuget package cache paths
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
        
            // Instantiate services
            GithubService githubService = null;
            if (options.enableGithubLicenses)
            {
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
        
            // Determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = this.fileSystem.Path.GetFullPath(SolutionOrProjectFile);
            await Console.Out.WriteLineAsync($"Scanning at {fullSolutionOrProjectFilePath}");
        
            var topLevelComponent = new Component
            {
                // Name is set below
                Version = string.IsNullOrEmpty(setVersion) ? "0.0.0" : setVersion,
                Type = setType == Component.Classification.Null ? Component.Classification.Application : setType
            };
        
            // Analysis logic here...
        
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
        
            // Create the BOM
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
        
            // Bom format handling
            string bomContents;
            switch (bomFormat)
            {
                case BomFormat.JSON:
                    bomContents = BomService.CreateDocument(bom, true);
                    break;
                case BomFormat.Protobuf:
                    bomContents = BomService.CreateProtobuf(bom);
                    break;
                case BomFormat.XML:
                default:
                    bomContents = BomService.CreateDocument(bom, false);
                    break;
            }
        
            // Check if the output directory exists and create it if needed
            var bomPath = this.fileSystem.Path.GetFullPath(outputDirectory);
            if (!this.fileSystem.Directory.Exists(bomPath))
            {
                this.fileSystem.Directory.CreateDirectory(bomPath);
            }
        
            // Write the BOM to disk
            var bomFilename = outputFilename;
            if (string.IsNullOrEmpty(bomFilename))
            {
                bomFilename = bomFormat == BomFormat.JSON ? "bom.json" : bomFormat == BomFormat.Protobuf ? "bom.protobuf" : "bom.xml";
            }
            var bomFilePath = this.fileSystem.Path.Combine(bomPath, bomFilename);
            Console.WriteLine("Writing to: " + bomFilePath);
            this.fileSystem.File.WriteAllText(bomFilePath, bomContents);
        
            return 0;
        }
        
        private static Bom ReadMetaDataFromFile(Bom bom, string templatePath)
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
            // Automatically generate a timestamp if none is provided with the metadata
            if (bom.Metadata.Timestamp == null)
            {
                bom.Metadata.Timestamp = DateTime.UtcNow;
            }
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
