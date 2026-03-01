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
using System.Text.RegularExpressions;
using System.Reflection;

namespace CycloneDX.Services
{
    public class DotnetRestoreException : Exception
    {
        public DotnetRestoreException(string message) : base(message) { }

        public DotnetRestoreException(string message, Exception innerException) : base(message, innerException) { }
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

        public bool IsTestProject(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                return false;
            }

            XmlDocument xmldoc = new XmlDocument();
            using var fileStream = _fileSystem.FileStream.New(projectFilePath, FileMode.Open, FileAccess.Read);
            xmldoc.Load(fileStream);
            string namespaceUri = xmldoc.DocumentElement.NamespaceURI;

            XmlNamespaceManager namespaces = new XmlNamespaceManager(xmldoc.NameTable);
            if (!string.IsNullOrEmpty(namespaceUri))
            {
                namespaces.AddNamespace("ns", namespaceUri);
            }

            string xpathPrefix = string.IsNullOrEmpty(namespaceUri) ? "" : "ns:";
            XmlElement testSdkReference = xmldoc.SelectSingleNode($"/{xpathPrefix}Project/{xpathPrefix}ItemGroup/{xpathPrefix}PackageReference[@Include='Microsoft.NET.Test.Sdk']", namespaces) as XmlElement;
            XmlElement testProjectPropertyGroup = xmldoc.SelectSingleNode($"/{xpathPrefix}Project/{xpathPrefix}PropertyGroup[{xpathPrefix}IsTestProject='true']", namespaces) as XmlElement;

            return testSdkReference != null || testProjectPropertyGroup != null;
        }

        public (string name, string version) GetAssemblyNameAndVersion(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                return (projectFilePath, "undefined");
            }


            XmlDocument xmldoc = new XmlDocument();
            using var fileStream = _fileSystem.FileStream.New(projectFilePath, FileMode.Open, FileAccess.Read);
            xmldoc.Load(fileStream);

            XmlNamespaceManager namespaces = new XmlNamespaceManager(xmldoc.NameTable);
            namespaces.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

            string name = (xmldoc.SelectSingleNode("/Project/PropertyGroup/AssemblyName") as XmlElement)?.InnerText;
            name ??= (xmldoc.SelectSingleNode("/Project/PropertyGroup/msbuild:AssemblyName", namespaces) as XmlElement)?.InnerText;
                

            if (name?.Contains("$(MSBuildProjectName)") == true)
            {
                var projectName = _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);
                name = name.Replace("$(MSBuildProjectName)", projectName);                
            }

            name ??= _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);


            // Extract Version — try each property in priority order before falling back to AssemblyInfo
            string version =
                (xmldoc.SelectSingleNode("/Project/PropertyGroup/Version") as XmlElement)?.InnerText ??
                (xmldoc.SelectSingleNode("/Project/PropertyGroup/AssemblyVersion") as XmlElement)?.InnerText ??
                (xmldoc.SelectSingleNode("/Project/PropertyGroup/ProjectVersion") as XmlElement)?.InnerText ??
                (xmldoc.SelectSingleNode("/Project/PropertyGroup/PackageVersion") as XmlElement)?.InnerText;

            if (version == null)
            {
                string assemblyInfoPath;
                string pattern;
                string projectFileExtension = Path.GetExtension(projectFilePath);
                if (projectFileExtension.Equals(".vbproj", StringComparison.Ordinal))
                {
                    assemblyInfoPath = Path.Combine(Path.GetDirectoryName(projectFilePath), "My Project", "AssemblyInfo.vb");
                    pattern = @"^\<Assembly: AssemblyVersion\(""(?<Version>.*?)""\)\>$";
                }
                else if (projectFileExtension.Equals(".xsproj", StringComparison.Ordinal))
                {
                    assemblyInfoPath = Path.Combine(Path.GetDirectoryName(projectFilePath), "Properties", "AssemblyInfo.prg");
                    pattern = @"^\[assembly: AssemblyVersion\(""(?<Version>.*?)""\)\]$";
                }
                else
                {
                    assemblyInfoPath = Path.Combine(Path.GetDirectoryName(projectFilePath), "Properties", "AssemblyInfo.cs");
                    pattern = @"^\[assembly: AssemblyVersion\(""(?<Version>.*?)""\)\]$";
                }

                if (_fileSystem.File.Exists(assemblyInfoPath))
                {
                    string[] lines = _fileSystem.File.ReadAllLines(assemblyInfoPath);
                    foreach (var line in lines)
                    {
                        Match match = Regex.Match(line, pattern);

                        if (match.Success)
                        {
                            version = match.Groups["Version"].Value;
                            break;
                        }
                    }
                }
            }

            return (name, version);
        }

        public bool DisablePackageRestore { get; set; }

        /// <summary>
        /// Analyzes a single Project file for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<DotnetDependency>> GetProjectDotnetDependencysAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                Console.Error.WriteLine($"Project file \"{projectFilePath}\" does not exist");
                return new HashSet<DotnetDependency>();
            }

            var isTestProject = IsTestProject(projectFilePath);

            Console.WriteLine();
            Console.WriteLine($"» Analyzing: {projectFilePath}");

            if (excludeTestProjects && isTestProject)
            {
                Console.WriteLine($"Skipping: {projectFilePath}");
                return new HashSet<DotnetDependency>();
            }

            if (!DisablePackageRestore)
            {
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

            var assetsFilename = GetProjectAssetsFilePath(projectFilePath, baseIntermediateOutputPath);
            if (!_fileSystem.File.Exists(assetsFilename))
            {
                Console.WriteLine($"File not found: \"{assetsFilename}\", \"{projectFilePath}\" ");
            }
            var packages = _projectAssetsFileService.GetDotnetDependencys(projectFilePath, assetsFilename, isTestProject);


            // if there are no project file package references look for a packages.config
            if (!packages.Any())
            {
                Console.WriteLine("  No packages found");
                var directoryPath = _fileSystem.Path.GetDirectoryName(projectFilePath);
                var packagesPath = _fileSystem.Path.Combine(directoryPath, "packages.config");
                if (_fileSystem.File.Exists(packagesPath))
                {
                    Console.WriteLine("  Found packages.config. Will attempt to process");
                    packages = await _packagesFileService.GetDotnetDependencysAsync(packagesPath).ConfigureAwait(false);
                }
            }
            return packages;
        }

        internal string GetProjectAssetsFilePath(string projectFilePath, string baseIntermediateOutputPath)
        {
            string assetsPath;

            if (string.IsNullOrEmpty(baseIntermediateOutputPath))
            {
                // Default to <projectDir>/obj
                assetsPath = Path.Combine(Path.GetDirectoryName(projectFilePath), "obj", "project.assets.json");
            }
            else
            {
                // Use <baseIntermediateOutputPath>/obj/<projectName>
                string projectName = Path.GetFileNameWithoutExtension(projectFilePath);
                assetsPath = Path.Combine(baseIntermediateOutputPath, "obj", projectName, "project.assets.json");
            }

            if (_fileSystem.File.Exists(assetsPath))
            {
                Console.WriteLine($"  Found Assetsfile under {assetsPath}");
                return assetsPath;
            }

            var result = _dotnetUtilsService.GetAssetsPath(projectFilePath);
            if (result.Success && _fileSystem.File.Exists(result.Result))
            {
                Console.WriteLine($"  Found Assetsfile under {result.Result}");
                return result.Result;
            }

            // Fall back to expected path even if file doesn't exist
            return assetsPath;
        }


        /// <summary>
        /// Analyzes all Project file references for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<DotnetDependency>> RecursivelyGetProjectDotnetDependencysAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime)
        {
            var dotnetDependencys = await GetProjectDotnetDependencysAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, framework, runtime).ConfigureAwait(false);
            foreach (var item in dotnetDependencys)
            {
                item.IsDirectReference = true;
            }

            // If the root project has a project.assets.json it is SDK-style (PackageReference).
            // NuGet already writes the full transitive closure — including packages from all
            // ProjectReference projects — into that single file, so --recursive adds no value
            // for package discovery. Suggest the user drop the flag.
            var assetsFilePath = GetProjectAssetsFilePath(projectFilePath, baseIntermediateOutputPath);
            if (_fileSystem.File.Exists(assetsFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine(
                    "Consider removing --recursive: the root project.assets.json already contains " +
                    "the full NuGet package closure for SDK-style (PackageReference) projects. " +
                    "--recursive is only needed when a referenced project uses packages.config, " +
                    "or when combined with --include-project-references (-ipr) to list " +
                    "referenced projects as BOM components.");
                Console.ResetColor();
            }

            var projectReferences = await RecursivelyGetProjectReferencesAsync(projectFilePath).ConfigureAwait(false);

            //Remove root-project, it will be added to the metadata
            var rootProject = projectReferences.FirstOrDefault(p => p.Path == projectFilePath);
            projectReferences.Where(p => rootProject.Dependencies.ContainsKey(p.Name)).ToList().ForEach(p => p.IsDirectReference = true);
            projectReferences.Remove(rootProject);

            foreach (var project in projectReferences)
            {
                var projectDotnetDependencys = await GetProjectDotnetDependencysAsync(project.Path, baseIntermediateOutputPath, excludeTestProjects, framework, runtime).ConfigureAwait(false);

                //Add dependencies for dependency graph
                foreach (var dependency in projectDotnetDependencys)
                {
                    if (project.Dependencies.TryGetValue(dependency.Name, out string version))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Warning: trying to add {dependency.Name}@{dependency.Version} as a dependency to {project.Name}, but it was already with version {version}");
                        Console.ResetColor();
                        continue;
                    }
                    project.Dependencies.Add(dependency.Name, dependency.Version);
                }

                dotnetDependencys.UnionWith(projectDotnetDependencys);
            }

            //When there is a project.assets.json, the project references are already added, so check before adding
            var allAddedDepedencyNames = dotnetDependencys.Select(dep => dep.Name);
            dotnetDependencys.UnionWith(projectReferences.Where(pr => !allAddedDepedencyNames.Contains(pr.Name)));

            return dotnetDependencys;
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
        public async Task<HashSet<DotnetDependency>> RecursivelyGetProjectReferencesAsync(string projectFilePath)
        {
            var projectReferences = new HashSet<DotnetDependency>();

            // Initialize the queue with the current project file
            var files = new Queue<string>();
            files.Enqueue(_fileSystem.FileInfo.New(projectFilePath).FullName);

            var visitedProjectFiles = new HashSet<string>();

            while (files.Count > 0)
            {
                var currentFile = files.Dequeue();

                if (!Utils.IsSupportedProjectType(currentFile))
                {
                    continue;
                }

                // Find all project references inside of currentFile
                var foundProjectReferences = await GetProjectReferencesAsync(currentFile).ConfigureAwait(false);

                var nameAndVersion = GetAssemblyNameAndVersion(currentFile);

                DotnetDependency dependency = new();
                dependency.Name = nameAndVersion.name;
                dependency.Version = nameAndVersion.version ?? "1.0.0"; //a project that has no version defined is listed as 1.0.0 in an assets-File
                dependency.Path = currentFile;
                dependency.Dependencies = foundProjectReferences.
                                          Select(GetAssemblyNameAndVersion).
                                          ToDictionary(project => project.name, project => project.version ?? "1.0.0");
                dependency.Scope = Component.ComponentScope.Required;
                dependency.DependencyType = DependencyType.Project;
                projectReferences.Add(dependency);

                // Add unvisited project files to the queue
                // Loop through found project references
                foreach (string projectReferencePath in foundProjectReferences)
                {
                    if (!visitedProjectFiles.Contains(projectReferencePath))
                    {
                        files.Enqueue(projectReferencePath);
                    }
                }

                // Add the currentFile to list of visited projects
                visitedProjectFiles.Add(currentFile);
            }

            return projectReferences;
        }



        public Component GetComponent(DotnetDependency dotnetDependency)
        {
            if (dotnetDependency?.DependencyType != DependencyType.Project)
            {
                return null;
            }
            var component = SetupComponent(dotnetDependency.Name, dotnetDependency.Version, Component.ComponentScope.Required);

            return component;
        }


        private Component SetupComponent(string name, string version, Component.ComponentScope? scope)
        {
            var component = new Component
            {
                Name = name,
                Version = version,
                Scope = scope,
                Type = Component.Classification.Library,
                BomRef = $"{name}@{version}"
            };
            return component;
        }
    }
}
