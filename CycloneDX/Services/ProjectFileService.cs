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

        // SDK-style projects evaluate Version to 1.0.0 when nothing sets it. That cannot be told
        // apart from an explicit 1.0.0, so it is treated as unset and the project file fallbacks apply.
        private const string MSBuildDefaultVersion = "1.0.0";

        // Requested together, so each project is evaluated once
        private static readonly string[] EvaluatedPropertyNames = { "Version", "PackageId", "AssemblyName", "IsTestProject", "ProjectAssetsFile" };
        private static readonly string[] EvaluatedItemTypes = { "PackageReference" };

        private IFileSystem _fileSystem;
        private IDotnetUtilsService _dotnetUtilsService;
        private IPackagesFileService _packagesFileService;
        private IProjectAssetsFileService _projectAssetsFileService;
        private readonly IProjectEvaluationService _projectEvaluationService;
        private readonly Dictionary<string, ProjectEvaluation> _projects = new Dictionary<string, ProjectEvaluation>(StringComparer.OrdinalIgnoreCase);
        // Projects found but not evaluated yet; the next evaluation includes all of them
        private readonly HashSet<string> _projectsToEvaluate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DotnetUtilsResult<EvaluatedProject>> _evaluations = new Dictionary<string, DotnetUtilsResult<EvaluatedProject>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reportedEvaluationErrors = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Reads projects from their project file XML only, without MSBuild evaluation.</summary>
        public ProjectFileService(
            IFileSystem fileSystem,
            IDotnetUtilsService dotnetUtilsService,
            IPackagesFileService packagesFileService,
            IProjectAssetsFileService projectAssetsFileService)
            : this(fileSystem, dotnetUtilsService, packagesFileService, projectAssetsFileService, null)
        {
        }

        public ProjectFileService(
            IFileSystem fileSystem,
            IDotnetUtilsService dotnetUtilsService,
            IPackagesFileService packagesFileService,
            IProjectAssetsFileService projectAssetsFileService,
            IProjectEvaluationService projectEvaluationService)
        {
            _fileSystem = fileSystem;
            _dotnetUtilsService = dotnetUtilsService;
            _packagesFileService = packagesFileService;
            _projectAssetsFileService = projectAssetsFileService;
            _projectEvaluationService = projectEvaluationService;
        }

        private ProjectEvaluation GetProject(string projectFilePath)
        {
            if (!_projects.TryGetValue(projectFilePath, out var project))
            {
                QueueEvaluation(new[] { projectFilePath });
                Func<DotnetUtilsResult<EvaluatedProject>> evaluate = _projectEvaluationService is null
                    ? null
                    : () => Evaluate(projectFilePath);
                project = new ProjectEvaluation(projectFilePath, _fileSystem, evaluate, error => ReportEvaluationError(projectFilePath, error));
                _projects[projectFilePath] = project;
            }
            return project;
        }

        public void QueueEvaluation(IEnumerable<string> projectFilePaths)
        {
            foreach (var projectFilePath in projectFilePaths)
            {
                if (!_evaluations.ContainsKey(projectFilePath))
                {
                    _projectsToEvaluate.Add(projectFilePath);
                }
            }
        }

        /// <summary>
        /// Evaluates the project together with all projects queued so far, because every evaluation
        /// starts a dotnet process; one per project would make large solutions slow.
        /// </summary>
        private DotnetUtilsResult<EvaluatedProject> Evaluate(string projectFilePath)
        {
            if (!_evaluations.TryGetValue(projectFilePath, out var evaluation))
            {
                _projectsToEvaluate.Add(projectFilePath);
                var projects = _projectsToEvaluate.ToList();
                _projectsToEvaluate.Clear();
                var results = _projectEvaluationService.EvaluateProjects(projects, EvaluatedPropertyNames, EvaluatedItemTypes, EvaluationFramework, EvaluationConfiguration);
                foreach (var project in projects)
                {
                    _evaluations[project] = results.TryGetValue(project, out var result)
                        ? result
                        : new DotnetUtilsResult<EvaluatedProject> { ErrorMessage = "MSBuild did not evaluate the project" };
                }
                evaluation = _evaluations[projectFilePath];
            }
            return evaluation;
        }

        /// <summary>
        /// Without this notice users cannot tell why values from e.g. Directory.Build.props are ignored.
        /// Names the project, because referenced projects are evaluated while another one is analyzed.
        /// The same error (e.g. an unknown switch of an SDK that is too old) is reported only once.
        /// </summary>
        private void ReportEvaluationError(string projectFilePath, string errorMessage)
        {
            if (!_reportedEvaluationErrors.Add(errorMessage ?? string.Empty))
            {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Could not evaluate properties of {Path.GetFileName(projectFilePath)} with MSBuild, reading the project file instead: {errorMessage}");
            Console.ResetColor();
        }

        public bool IsTestProject(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                return false;
            }

            var project = GetProject(projectFilePath);
            return string.Equals(project.GetProperty("IsTestProject"), "true", StringComparison.OrdinalIgnoreCase)
                || project.GetItems("PackageReference").Contains("Microsoft.NET.Test.Sdk", StringComparer.OrdinalIgnoreCase);
        }

        public (string name, string version) GetAssemblyNameAndVersion(string projectFilePath)
        {
            if (!_fileSystem.File.Exists(projectFilePath))
            {
                return (projectFilePath, "undefined");
            }


            var project = GetProject(projectFilePath);

            // NuGet names project references by PackageId in project.assets.json, so use the same name
            string name = project.GetEvaluatedProperty("PackageId")
                ?? project.GetEvaluatedProperty("AssemblyName")
                ?? project.GetDeclaredProperty("AssemblyName");
                

            if (name?.Contains("$(RootNamespace)") == true)
            {
                // <AssemblyName>$(RootNamespace)</AssemblyName> is a common idiom, but the literal
                // token is identical across every project that uses it. Left unresolved, two such
                // projects collide as the same dictionary key in RecursivelyGetProjectReferencesAsync
                // (see issue #917). Use best-effort substitution from the project's own
                // <RootNamespace> element, not full MSBuild evaluation; guard against
                // <RootNamespace>$(AssemblyName)</RootNamespace>
                // (the documented workaround for this same issue) so the two can't resolve each
                // other into an infinite loop.
                string rootNamespace = project.GetDeclaredProperty("RootNamespace");

                var projectName = _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);
                if (!string.IsNullOrEmpty(rootNamespace) && !rootNamespace.Contains("$(AssemblyName)"))
                {
                    name = name.Replace("$(RootNamespace)", rootNamespace);
                }
                else
                {
                    name = name.Replace("$(RootNamespace)", projectName);
                }
            }

            // RootNamespace substitution can introduce this known token too.
            if (name?.Contains("$(MSBuildProjectName)") == true)
            {
                var projectName = _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);
                name = name.Replace("$(MSBuildProjectName)", projectName);
            }

            name ??= _fileSystem.Path.GetFileNameWithoutExtension(projectFilePath);


            // Extract Version — evaluated, else try each declared property in priority order before falling back to AssemblyInfo
            string version = project.GetEvaluatedProperty("Version");
            if (version == MSBuildDefaultVersion)
            {
                version = null;
            }
            version ??=
                project.GetDeclaredProperty("Version") ??
                project.GetDeclaredProperty("AssemblyVersion") ??
                project.GetDeclaredProperty("ProjectVersion") ??
                project.GetDeclaredProperty("PackageVersion");

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

        /// <summary>Target framework passed to MSBuild when evaluating project properties.</summary>
        public string EvaluationFramework { get; set; }

        /// <summary>Configuration passed to MSBuild when evaluating project properties.</summary>
        public string EvaluationConfiguration { get; set; }

        /// <summary>
        /// Analyzes a single Project file for NuGet package references.
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<DotnetDependency>> GetProjectDotnetDependencysAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime)
        {
            return await GetProjectDotnetDependencysAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, framework, runtime, null).ConfigureAwait(false);
        }

        public async Task<HashSet<DotnetDependency>> GetProjectDotnetDependencysAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime, string configuration)
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
                var restoreResult = _dotnetUtilsService.Restore(projectFilePath, framework, runtime, configuration);

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
                    if (isTestProject)
                    {
                        foreach (var package in packages)
                        {
                            package.IsRuntimeReachable = false;
                            package.Scope = Component.ComponentScope.Excluded;
                        }
                    }
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

            var project = GetProject(projectFilePath);
            var evaluatedAssetsPath = project.GetEvaluatedProperty("ProjectAssetsFile");
            if (evaluatedAssetsPath != null && _fileSystem.File.Exists(evaluatedAssetsPath))
            {
                Console.WriteLine($"  Found Assetsfile under {evaluatedAssetsPath}");
                return evaluatedAssetsPath;
            }

            // Only without an evaluation ask the dotnet CLI, which takes a process per project
            if (!project.IsEvaluated)
            {
                var result = _dotnetUtilsService.GetAssetsPath(projectFilePath);
                if (result.Success && _fileSystem.File.Exists(result.Result))
                {
                    Console.WriteLine($"  Found Assetsfile under {result.Result}");
                    return result.Result;
                }
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
            return await RecursivelyGetProjectDotnetDependencysAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, framework, runtime, null).ConfigureAwait(false);
        }

        public async Task<HashSet<DotnetDependency>> RecursivelyGetProjectDotnetDependencysAsync(string projectFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime, string configuration)
        {
            var dotnetDependencys = await GetProjectDotnetDependencysAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, framework, runtime, configuration).ConfigureAwait(false);
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
                var projectDotnetDependencys = await GetProjectDotnetDependencysAsync(project.Path, baseIntermediateOutputPath, excludeTestProjects, framework, runtime, configuration).ConfigureAwait(false);

                //Add dependencies for dependency graph
                foreach (var dependency in projectDotnetDependencys)
                {
                    project.AddDependencyEdge(dependency.Name, dependency.Version);
                }

                DotnetDependency.MergeInto(dotnetDependencys, projectDotnetDependencys);
            }

            //When there is a project.assets.json, the project references are already added, so check before adding
            var allAddedDepedencyNames = dotnetDependencys.Select(dep => dep.Name);
            DotnetDependency.MergeInto(dotnetDependencys, projectReferences.Where(pr => !allAddedDepedencyNames.Contains(pr.Name)));

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
            QueueEvaluation(projectReferences.Where(_fileSystem.File.Exists));

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
