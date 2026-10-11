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
            using NuGet.Versioning;
            
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
                        string configuration = options.configuration;
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
                                packages = await solutionFileService.GetSolutionDotnetDependencys(fullSolutionOrProjectFilePath, baseIntermediateOutputPath, excludetestprojects, framework, runtime, configu[...