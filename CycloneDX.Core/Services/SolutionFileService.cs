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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO.Abstractions;
using CycloneDX.Core.Models;

namespace CycloneDX.Services
{
    public class SolutionFileService : ISolutionFileService
    {
        private readonly IFileSystem _fileSystem;
        private readonly IProjectFileService _projectFileService;

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
                    if (!match.Success) 
                        continue;

                    var relativeProjectPath = match.Groups[3].Value.Replace('\\', _fileSystem.Path.DirectorySeparatorChar);
                    var projectFile = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(solutionFolder, relativeProjectPath));
                    if (Core.Utils.IsSupportedProjectType(projectFile)) projects.Add(projectFile);
                }
            }

            var projectList = new List<string>(projects);
            foreach (var project in projectList)
            {
                var projectReferences = await _projectFileService.RecursivelyGetProjectReferencesAsync(project).ConfigureAwait(false);
                projects.UnionWith(projectReferences);
            }

            return projects;
        }

        /// <summary>
        /// Analyzes a single Solution file for NuGet package references in referenced project files
        /// </summary>
        /// <param name="solutionFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<NugetPackage>> GetSolutionNugetPackages(string solutionFilePath)
        {
            if (!_fileSystem.File.Exists(solutionFilePath))
            {
                await Console.Error.WriteLineAsync($"Solution file \"{solutionFilePath}\" does not exist").ConfigureAwait(false);
                return new HashSet<NugetPackage>();
            }

            Console.WriteLine();
            Console.WriteLine($"» Solution: {solutionFilePath}");
            Console.WriteLine("  Getting projects");

            var packages = new HashSet<NugetPackage>();

            var projectPaths = await GetSolutionProjectReferencesAsync(solutionFilePath).ConfigureAwait(false);

            if (projectPaths.Count == 0)
            {
                await Console.Error.WriteLineAsync("  No projects found").ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine($"  {projectPaths.Count} project(s) found");
            }

            foreach (var projectFilePath in projectPaths)
            {
                Console.WriteLine();
                var projectPackages = await _projectFileService.GetProjectNugetPackagesAsync(projectFilePath).ConfigureAwait(false);
                packages.UnionWith(projectPackages);
            }

            return packages;
        }
    }
}