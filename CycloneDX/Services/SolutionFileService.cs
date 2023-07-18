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
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Buildalyzer;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class SolutionFileService : ISolutionFileService
    {
        private IFileSystem _fileSystem;
        private IProjectFileService _projectFileService;

        public SolutionFileService(IFileSystem fileSystem, IProjectFileService projectFileService)
        {
            _fileSystem = fileSystem;
            _projectFileService = projectFileService;
        }

        /// <summary>
        /// Analyze a single Solution file for all project references.
        /// </summary>
        /// <param name="solutionFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<string>> GetSolutionProjectReferencesAsync(string solutionFilePath)
        {
            var solutionFolder = _fileSystem.Path.GetDirectoryName(solutionFilePath);
            var projects = new HashSet<string>();
            using (var reader = _fileSystem.File.OpenText(solutionFilePath))
            {
                string line;

                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (!line.StartsWith("Project", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var regex = new Regex("(.*) = \"(.*?)\", \"(.*?)\"");
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        var relativeProjectPath = match.Groups[3].Value.Replace('\\', _fileSystem.Path.DirectorySeparatorChar);
                        var projectFile = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(solutionFolder, relativeProjectPath));
                        if (Utils.IsSupportedProjectType(projectFile)) projects.Add(projectFile);
                    }
                }
            }

            var projectList = new List<string>(projects);
            foreach (var project in projectList)
            {
                var projectReferences = await _projectFileService.RecursivelyGetProjectReferencesAsync(project).ConfigureAwait(false);
                projects.UnionWith(projectReferences.Select(dep => dep.Path));
            }

            return projects;
        }

        /// <summary>
        /// Analyzes a single Solution file for NuGet package references in referenced project files
        /// </summary>
        /// <param name="solutionFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<DotnetDependency>> GetSolutionDotnetDependencys(string solutionFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, string framework, string runtime)
        {
            if (!_fileSystem.File.Exists(solutionFilePath))
            {
                Console.Error.WriteLine($"Solution file \"{solutionFilePath}\" does not exist");
                return new HashSet<DotnetDependency>();
            }

            Console.WriteLine();
            Console.WriteLine($"» Solution: {solutionFilePath}");
            Console.WriteLine("  Getting projects");

            var packages = new HashSet<DotnetDependency>();

            HashSet<(string path,bool isTestProject)> projectPaths = new();

            AnalyzerManager manager = new AnalyzerManager(solutionFilePath);
            foreach (var (key, project) in manager.Projects)
            {
                var buildResults = project.Build();
                var isTestProject = buildResults.SelectMany(x => x.Properties)
                    .Any(x => x.Key.Equals("IsTestProject", StringComparison.OrdinalIgnoreCase) && x.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

                projectPaths.Add((project.ProjectFile.Path, isTestProject));
            }

            if (projectPaths.Count == 0)
            {
                Console.Error.WriteLine("  No projects found");
            }
            else
            {
                Console.WriteLine($"  {projectPaths.Count} project(s) found");
            }

            // Process first all productive projects, then test projects (scope order)
            var projectQuery = from p in projectPaths orderby p.isTestProject select p;
            var directReferencePackages = new HashSet<DotnetDependency>();
            foreach (var (projectFilePath, isTest) in projectQuery)
            {
                Console.WriteLine();
                var projectPackages = await _projectFileService.GetProjectDotnetDependencysAsync(projectFilePath, baseIntermediateOutputPath, excludeTestProjects, framework, runtime, isTest).ConfigureAwait(false);
                directReferencePackages.UnionWith(projectPackages.Where(p => p.IsDirectReference));
                packages.UnionWith(projectPackages);
            }

            // Ensure packages which were discovered later are reflected as direct references in the final list.
            foreach (var directPackage in directReferencePackages)
            {
                packages.TryGetValue(directPackage, out var package);
                package.IsDirectReference = true;
            }

            return packages;
        }
    }
}
