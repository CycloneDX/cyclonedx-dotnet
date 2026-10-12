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
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    /// <summary>
    /// Evaluates projects with "dotnet msbuild" and a generated helper project. The helper project calls a
    /// target in every project that returns the requested values. The target is added to the projects
    /// through the CustomAfterMicrosoftCommon(CrossTargeting)Targets hooks of the .NET SDK.
    /// </summary>
    public sealed class ProjectEvaluationService : IProjectEvaluationService
    {
        private const string EvaluateTarget = "CycloneDXEvaluate";
        private const string GetValuesTarget = "CycloneDXGetProjectValues";

        private readonly IFileSystem _fileSystem;
        private readonly IDotnetCommandService _dotnetCommandService;

        public ProjectEvaluationService(IFileSystem fileSystem, IDotnetCommandService dotnetCommandService)
        {
            _fileSystem = fileSystem;
            _dotnetCommandService = dotnetCommandService;
        }

        public Dictionary<string, DotnetUtilsResult<EvaluatedProject>> EvaluateProjects(
            IEnumerable<string> projectFilePaths, IEnumerable<string> propertyNames, IEnumerable<string> itemTypes,
            string framework, string configuration)
        {
            var projects = projectFilePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (projects.Count == 0)
            {
                return new Dictionary<string, DotnetUtilsResult<EvaluatedProject>>(StringComparer.OrdinalIgnoreCase);
            }

            var fullPaths = projects.ToDictionary(project => project, _fileSystem.Path.GetFullPath, StringComparer.OrdinalIgnoreCase);
            var evaluateProject = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "cyclonedx-" + Guid.NewGuid().ToString("N"), "evaluate.proj");
            var commandResult = Run(evaluateProject, fullPaths.Values, propertyNames, itemTypes, framework, configuration);
            var output = commandResult.StdOut ?? "";
            // -getTargetResult writes the JSON after the log
            var jsonStart = ("\n" + output).IndexOf("\n{", StringComparison.Ordinal);
            var evaluatedProjects = jsonStart < 0
                ? new Dictionary<string, EvaluatedProject>()
                : ParseTargetResult(output.Substring(jsonStart), propertyNames.ToList(), itemTypes.ToList());

            var logLines = ((jsonStart < 0 ? output : output.Substring(0, jsonStart)) + "\n" + commandResult.StdErr)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToList();
            var errors = logLines
                .Where(line => line.Contains(": error ", StringComparison.Ordinal))
                .Select(line => WithoutLocationIn(line, evaluateProject))
                .ToList();
            if (errors.Count == 0 && !commandResult.Success)
            {
                // e.g. the timeout message
                errors = logLines;
            }

            return projects.ToDictionary(
                project => project,
                project => evaluatedProjects.TryGetValue(fullPaths[project], out var evaluatedProject)
                    ? new DotnetUtilsResult<EvaluatedProject> { Result = evaluatedProject }
                    : new DotnetUtilsResult<EvaluatedProject> { ErrorMessage = GetErrorMessage(fullPaths[project], fullPaths.Values, errors) },
                StringComparer.OrdinalIgnoreCase);
        }

        private DotnetCommandResult Run(string evaluateProject, IEnumerable<string> projects, IEnumerable<string> propertyNames,
            IEnumerable<string> itemTypes, string framework, string configuration)
        {
            var directory = _fileSystem.Path.GetDirectoryName(evaluateProject);
            _fileSystem.Directory.CreateDirectory(directory);
            try
            {
                _fileSystem.File.WriteAllText(_fileSystem.Path.Combine(directory, "cyclonedx.targets"), CreateTargets(propertyNames, itemTypes));
                _fileSystem.File.WriteAllText(evaluateProject, CreateEvaluateProject(projects));

                var arguments = $"msbuild \"{evaluateProject}\" -nologo -v:quiet -nodeReuse:false -getTargetResult:{EvaluateTarget}";
                if (!string.IsNullOrEmpty(framework))
                {
                    arguments = $"{arguments} -p:TargetFramework={framework}";
                }
                if (!string.IsNullOrEmpty(configuration))
                {
                    arguments = $"{arguments} -p:Configuration={configuration}";
                }
                return _dotnetCommandService.Run(arguments);
            }
            finally
            {
                try
                {
                    _fileSystem.Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                    // only a few small files in the temp directory are left behind
                }
            }
        }

        // Imported into every project; returns the project path with the values as metadata
        private static string CreateTargets(IEnumerable<string> propertyNames, IEnumerable<string> itemTypes)
        {
            var values = new XElement("_CycloneDXProjectValues", new XAttribute("Include", "$(MSBuildProjectFullPath)"));
            values.Add(propertyNames.Select(name => new XAttribute(name, $"$({name})")));
            values.Add(itemTypes.Select(itemType => new XAttribute(itemType, $"@({itemType})")));
            return new XElement("Project",
                new XElement("Target",
                    new XAttribute("Name", GetValuesTarget),
                    new XAttribute("Returns", "@(_CycloneDXProjectValues)"),
                    new XElement("ItemGroup", values))).ToString();
        }

        // Calls the target in each project separately, so that a project that cannot be loaded does not stop the others
        private static string CreateEvaluateProject(IEnumerable<string> projects)
        {
            const string targets = "$(MSBuildThisFileDirectory)cyclonedx.targets";
            return new XElement("Project",
                new XElement("ItemGroup", projects.Select(project =>
                    new XElement("ProjectToEvaluate", new XAttribute("Include", EscapeForMsbuild(project))))),
                new XElement("Target",
                    new XAttribute("Name", EvaluateTarget),
                    new XAttribute("Returns", "@(EvaluatedProject)"),
                    new XElement("MSBuild",
                        new XAttribute("Projects", "%(ProjectToEvaluate.Identity)"),
                        new XAttribute("Targets", GetValuesTarget),
                        new XAttribute("Properties", $"CustomAfterMicrosoftCommonTargets={targets};CustomAfterMicrosoftCommonCrossTargetingTargets={targets}"),
                        new XAttribute("ContinueOnError", "ErrorAndContinue"),
                        new XElement("Output",
                            new XAttribute("TaskParameter", "TargetOutputs"),
                            new XAttribute("ItemName", "EvaluatedProject"))))).ToString();
        }

        // MSBuild would otherwise expand these characters in the item include, e.g. split the path at a ';'
        private static string EscapeForMsbuild(string value)
        {
            var escaped = new StringBuilder();
            foreach (var character in value)
            {
                escaped.Append("%$@;'?*".Contains(character)
                    ? $"%{(int)character:X2}"
                    : character.ToString());
            }
            return escaped.ToString();
        }

        /// <summary>The evaluated projects by full path.</summary>
        private static Dictionary<string, EvaluatedProject> ParseTargetResult(string json, List<string> propertyNames, List<string> itemTypes)
        {
            var evaluatedProjects = new Dictionary<string, EvaluatedProject>(StringComparer.OrdinalIgnoreCase);
            JsonDocument document;
            try
            {
                // reads only the JSON value, in case MSBuild writes something after it
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
                document = JsonDocument.ParseValue(ref reader);
            }
            catch (JsonException)
            {
                return evaluatedProjects;
            }

            using (document)
            {
                if (!document.RootElement.TryGetProperty("TargetResults", out var targetResults)
                    || !targetResults.TryGetProperty(EvaluateTarget, out var targetResult)
                    || !targetResult.TryGetProperty("Items", out var items))
                {
                    return evaluatedProjects;
                }
                foreach (var item in items.EnumerateArray().Where(item => item.TryGetProperty("Identity", out _)))
                {
                    var evaluatedProject = new EvaluatedProject();
                    foreach (var name in propertyNames.Where(name => item.TryGetProperty(name, out _)))
                    {
                        evaluatedProject.Properties[name] = item.GetProperty(name).GetString();
                    }
                    foreach (var itemType in itemTypes.Where(itemType => item.TryGetProperty(itemType, out _)))
                    {
                        evaluatedProject.Items[itemType] = item.GetProperty(itemType).GetString()
                            .Split(';', StringSplitOptions.RemoveEmptyEntries);
                    }
                    evaluatedProjects[item.GetProperty("Identity").GetString()] = evaluatedProject;
                }
            }
            return evaluatedProjects;
        }

        // The generated project is deleted, so its path and line only confuse, e.g. for a missing project file
        private static string WithoutLocationIn(string line, string file)
        {
            var messageStart = line.StartsWith(file, StringComparison.OrdinalIgnoreCase)
                ? line.IndexOf(": ", file.Length, StringComparison.Ordinal)
                : -1;
            return messageStart < 0 ? line : line.Substring(messageStart + 2);
        }

        /// <summary>
        /// The error MSBuild reported for the project; otherwise an error that names none of the projects, such
        /// as an unknown command line switch of an older .NET SDK.
        /// </summary>
        private static string GetErrorMessage(string project, IEnumerable<string> projects, List<string> errors)
        {
            return errors.FirstOrDefault(error => error.Contains(project, StringComparison.OrdinalIgnoreCase))
                ?? errors.FirstOrDefault(error => !projects.Any(other => error.Contains(other, StringComparison.OrdinalIgnoreCase)))
                ?? "MSBuild did not evaluate the project";
        }
    }
}
