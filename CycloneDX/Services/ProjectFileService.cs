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
using System.Linq;
using System.Xml;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class ProjectFileService
    {
        private XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings 
        {
            Async = true
        };
        
        private IFileSystem _fileSystem;
        private PackagesFileService _packagesFileService;

        public ProjectFileService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _packagesFileService = new PackagesFileService(_fileSystem);
        }

        /// <summary>
        /// Analyzes a single Project file for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> GetProjectNugetPackagesAsync(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                Console.Error.WriteLine($"Project file \"{projectFilePath}\" does not exist");
                return new HashSet<NugetPackage>();
            }

            Console.WriteLine();
            Console.WriteLine($"» Analyzing: {projectFilePath}");
            Console.WriteLine("  Getting packages");
            var packages = new HashSet<NugetPackage>();
            using (StreamReader fileReader = _fileSystem.File.OpenText(projectFilePath))
            {
                using (XmlReader reader = XmlReader.Create(fileReader, _xmlReaderSettings))
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.IsStartElement() && reader.Name == "PackageReference")
                        {
                            var package = new NugetPackage
                            {
                                Name = reader["Include"],
                                Version = reader["Version"],
                            };

                            if (!string.IsNullOrEmpty(package.Name) && !string.IsNullOrEmpty(package.Version))
                            {
                                packages.Add(package);
                            }
                        }
                    }
                }
            }
            // if there are no project file package references look for a packages.config
            if (!packages.Any())
            {
                Console.WriteLine("  No packages found");
                var directoryPath = _fileSystem.Path.GetDirectoryName(projectFilePath);
                var packagesPath = _fileSystem.Path.Combine(directoryPath, "packages.config");
                if (_fileSystem.File.Exists(packagesPath))
                {
                    Console.WriteLine("  Found packages.config. Will attempt to process");
                    packages = await _packagesFileService.GetNugetPackagesAsync(packagesPath);
                }
            }
            return packages;
        }

        /// <summary>
        /// Analyzes all Project file references for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> RecursivelyGetProjectNugetPackagesAsync(string projectFilePath)
        {
            var nugetPackages = await GetProjectNugetPackagesAsync(projectFilePath);
            var projectReferences = await RecursivelyGetProjectReferencesAsync(projectFilePath);
            foreach (var project in projectReferences)
            {
                var projectNugetPackages = await GetProjectNugetPackagesAsync(project);
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
            var projectDirectory = _fileSystem.FileInfo.FromFileName(projectFilePath).Directory.FullName;

            using (StreamReader fileReader = _fileSystem.File.OpenText(projectFilePath))
            {
                using (XmlReader reader = XmlReader.Create(fileReader, _xmlReaderSettings))
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.IsStartElement() && reader.Name == "ProjectReference")
                        {
                            var relativeProjectReference = 
                                reader["Include"].Replace('\\', _fileSystem.Path.DirectorySeparatorChar);
                            var fullProjectReference = _fileSystem.Path.Combine(projectDirectory, relativeProjectReference);
                            var absoluteProjectReference = _fileSystem.Path.GetFullPath(fullProjectReference);
                            projectReferences.Add(absoluteProjectReference);
                        }
                    }
                }
            }

            if (projectReferences.Count == 0)
            {
                Console.Error.WriteLine("  No project references found");
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
            files.Enqueue(_fileSystem.FileInfo.FromFileName(projectFilePath).FullName);
            string currentFile;

            var visitedProjectFiles = new HashSet<string>();

            while (files.TryDequeue(out currentFile))
            {
                // Find all project references inside of currentFile
                var foundProjectReferences = await GetProjectReferencesAsync(currentFile);

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