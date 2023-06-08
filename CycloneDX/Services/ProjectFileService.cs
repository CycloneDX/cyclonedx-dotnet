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
using System.Linq;
using System.Xml;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class DotnetRestoreException : Exception
    {
        public DotnetRestoreException(string message) : base(message) {}

        public DotnetRestoreException(string message, Exception innerException) : base(message, innerException) {}
    }

    public class ProjectFileService : IProjectFileService
    {
        private XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings
        {
            Async = true
        };

        private IFileSystem _fileSystem;
        private IDotnetUtilsService _dotnetUtilsService;
        private IPackagesFileService _packagesFileService;
        private IProjectAssetsFileService _projectAssetsFileService;

        public ProjectFileService(
            IFileSystem fileSystem,
            IDotnetUtilsService dotnetUtilsService,
            IPackagesFileService packagesFileService,
            IProjectAssetsFileService projectAssetsFileService)
        {
            _fileSystem = fileSystem;
            _dotnetUtilsService = dotnetUtilsService;
            _packagesFileService = packagesFileService;
            _projectAssetsFileService = projectAssetsFileService;
        }


        public static bool IsTestProject(string projectFilePath)
        {
            XmlDocument xmldoc = new XmlDocument();
            try
            {
                xmldoc.Load(projectFilePath);
            }
            catch(DirectoryNotFoundException /*ex*/)
            {
                // can only happen while testing (because it will be checked before this method is called)
                return false;
            }
            catch (FileNotFoundException /*ex*/)
            {
                // can only happen while testing (because it will be checked before this method is called)
                return false;
            }

            XmlElement testSdkReference = xmldoc.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='Microsoft.NET.Test.Sdk']") as XmlElement;
            if (testSdkReference != null)
            {
                return true;
            }
            
            XmlElement testProjectPropertyGroup = xmldoc.SelectSingleNode("/Project/PropertyGroup[IsTestProject='true']") as XmlElement;
            return testProjectPropertyGroup != null;
        }

        static internal String GetProjectProperty(string projectFilePath, string baseIntermediateOutputPath)
        {
            if (string.IsNullOrEmpty(baseIntermediateOutputPath))
            {
                return Path.Combine(Path.GetDirectoryName(projectFilePath), "obj");
            }
            else
            {
                string folderName = Path.GetFileNameWithoutExtension(projectFilePath);
                return Path.Combine(baseIntermediateOutputPath, "obj", folderName);
            }
        }

        public bool DisablePackageRestore { get; set; }

        /// <summary>
        /// Analyzes a single Project file for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> GetProjectNugetPackagesAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, bool excludeDev, string framework, string runtime)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                Console.Error.WriteLine($"Project file \"{projectFilePath}\" does not exist");
                return new HashSet<NugetPackage>();
            }

            var isTestProject = IsTestProject(projectFilePath);

            Console.WriteLine();
            Console.WriteLine($"» Analyzing: {projectFilePath}");

            if (excludeTestProjects && isTestProject)
            {
                Console.WriteLine($"Skipping: {projectFilePath}");
                return new HashSet<NugetPackage>();
            }

            if (!DisablePackageRestore) {
                Console.WriteLine("  Attempting to restore packages");
                var restoreResult = _dotnetUtilsService.Restore(projectFilePath, framework, runtime);

                if (restoreResult.Success)
                {
                    Console.WriteLine("  Packages restored");
                }
                else
                {
                    Console.WriteLine("Dotnet restore failed:");
                    Console.WriteLine(restoreResult.ErrorMessage);
                    throw new DotnetRestoreException($"Dotnet restore failed with message: {restoreResult.ErrorMessage}");
                }
            }

            var assetsFilename = _fileSystem.Path.Combine(GetProjectProperty(projectFilePath, baseIntermediateOutputPath), "project.assets.json");
            if (!File.Exists(assetsFilename))
            {
                Console.WriteLine($"File not found: \"{assetsFilename}\", \"{projectFilePath}\" ");
            }
            var packages = _projectAssetsFileService.GetNugetPackages(projectFilePath, assetsFilename, isTestProject, excludeDev);


            // if there are no project file package references look for a packages.config
            if (!packages.Any())
            {
                Console.WriteLine("  No packages found");
                var directoryPath = _fileSystem.Path.GetDirectoryName(projectFilePath);
                var packagesPath = _fileSystem.Path.Combine(directoryPath, "packages.config");
                if (_fileSystem.File.Exists(packagesPath))
                {
                    Console.WriteLine("  Found packages.config. Will attempt to process");
                    packages = await _packagesFileService.GetNugetPackagesAsync(packagesPath).ConfigureAwait(false);
                }
            }
            return packages;
        }

        /// <summary>
        /// Analyzes all Project file references for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> RecursivelyGetProjectNugetPackagesAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, bool excludeDev, string framework, string runtime)
        {
            var nugetPackages = await GetProjectNugetPackagesAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, excludeDev, framework, runtime).ConfigureAwait(false);
            var projectReferences = await RecursivelyGetProjectReferencesAsync(projectFilePath).ConfigureAwait(false);
            foreach (var project in projectReferences)
            {
                var projectNugetPackages = await GetProjectNugetPackagesAsync(project, baseIntermediateOutputPath, excludeTestProjects, excludeDev, framework, runtime).ConfigureAwait(false);
                nugetPackages.UnionWith(projectNugetPackages);
            }
            return nugetPackages;
        }

        /// <summary>
        /// Analyzes a single project file for references to other project files.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<string>> GetProjectReferencesAsync(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                Console.Error.WriteLine($"Project file \"{projectFilePath}\" does not exist");
                return new HashSet<string>();
            }

            Console.WriteLine();
            Console.WriteLine($"» Analyzing: {projectFilePath}");
            Console.WriteLine("  Getting project references");

            var projectReferences = new HashSet<string>();
            var projectDirectory = _fileSystem.FileInfo.New(projectFilePath).Directory.FullName;

            using (StreamReader fileReader = _fileSystem.File.OpenText(projectFilePath))
            {
                using (XmlReader reader = XmlReader.Create(fileReader, _xmlReaderSettings))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (reader.IsStartElement() && reader.Name == "ProjectReference")
                        {
                            string includeAttribute = reader["Include"];

                            if (includeAttribute != null)
                            {
                                var relativeProjectReference =
                                    includeAttribute.Replace('\\', _fileSystem.Path.DirectorySeparatorChar);
                                var fullProjectReference =
                                    _fileSystem.Path.Combine(projectDirectory, relativeProjectReference);
                                var absoluteProjectReference = _fileSystem.Path.GetFullPath(fullProjectReference);
                                projectReferences.Add(absoluteProjectReference);
                            }
                        }
                    }
                }
            }

            if (projectReferences.Count == 0)
            {
                Console.WriteLine("  No project references found");
            }

            return projectReferences;
        }

        /// <summary>
        /// Recursively analyzes project files and any referenced project files.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<string>> RecursivelyGetProjectReferencesAsync(string projectFilePath)
        {
            var projectReferences = new HashSet<string>();

            // Initialize the queue with the current project file
            var files = new Queue<string>();
            files.Enqueue(_fileSystem.FileInfo.New(projectFilePath).FullName);

            var visitedProjectFiles = new HashSet<string>();

            while (files.Count > 0)
            {
                var currentFile = files.Dequeue();
                // Find all project references inside of currentFile
                var foundProjectReferences = await GetProjectReferencesAsync(currentFile).ConfigureAwait(false);

                // Add unvisited project files to the queue
                // Loop through found project references
                foreach (string projectReferencePath in foundProjectReferences)
                {
                    if (!visitedProjectFiles.Contains(projectReferencePath))
                    {
                        files.Enqueue(projectReferencePath);
                        projectReferences.Add(projectReferencePath);
                    }
                }

                // Add the currentFile to list of visited projects
                visitedProjectFiles.Add(currentFile);
            }

            return projectReferences;
        }
    }
}
