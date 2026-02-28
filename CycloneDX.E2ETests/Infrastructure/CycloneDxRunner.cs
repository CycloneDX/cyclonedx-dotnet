// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static CycloneDX.E2ETests.Infrastructure.ToolFixture;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Invokes the published CycloneDX tool as a subprocess and captures output.
    /// </summary>
    internal sealed class CycloneDxRunner
    {
        private readonly string _toolDllPath;

        public CycloneDxRunner(string toolDllPath)
        {
            _toolDllPath = toolDllPath ?? throw new ArgumentNullException(nameof(toolDllPath));
        }

        /// <summary>
        /// Runs the CycloneDX tool against <paramref name="projectOrSolutionPath"/> and
        /// writes output into <paramref name="outputDir"/>.
        /// </summary>
        public async Task<ToolRunResult> RunAsync(
            string projectOrSolutionPath,
            string outputDir,
            ToolRunOptions options = null)
        {
            options ??= new ToolRunOptions();

            // Canonicalise paths before passing them further to break taint chains.
            var resolvedProjectPath = Path.GetFullPath(projectOrSolutionPath);
            var resolvedOutputDir = Path.GetFullPath(outputDir);

            var args = BuildArgs(resolvedProjectPath, resolvedOutputDir, options);

            var (exitCode, stdOut, stdErr) = await RunProcessAsync(
                "dotnet",
                args,
                workingDir: Path.GetDirectoryName(resolvedProjectPath)
            ).ConfigureAwait(false);

            // Find the generated BOM file
            string outputFilePath = null;
            string bomContent = null;

            if (exitCode == 0)
            {
                var filename = options.OutputFilename;
                if (filename == null)
                {
                    // Auto-detect: tool defaults to bom.xml or bom.json.
                    // Use GetFullPath + boundary check to ensure we stay within outputDir.
                    var xmlPath = Path.GetFullPath(Path.Combine(resolvedOutputDir, "bom.xml"));
                    var jsonPath = Path.GetFullPath(Path.Combine(resolvedOutputDir, "bom.json"));
                    if (xmlPath.StartsWith(resolvedOutputDir, StringComparison.Ordinal) && File.Exists(xmlPath)) // codeql[cs/path-injection]
                    {
                        outputFilePath = xmlPath;
                    }
                    else if (jsonPath.StartsWith(resolvedOutputDir, StringComparison.Ordinal) && File.Exists(jsonPath)) // codeql[cs/path-injection]
                    {
                        outputFilePath = jsonPath;
                    }
                }
                else
                {
                    var candidate = Path.GetFullPath(Path.Combine(resolvedOutputDir, filename));
                    if (!candidate.StartsWith(resolvedOutputDir, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Output filename '{filename}' escapes the output directory.");
                    }
                    outputFilePath = candidate;
                }

                if (outputFilePath != null && File.Exists(outputFilePath))
                {
                    bomContent = await File.ReadAllTextAsync(outputFilePath).ConfigureAwait(false);
                }
            }

            return new ToolRunResult(exitCode, stdOut, stdErr, outputFilePath, bomContent);
        }

        private IEnumerable<string> BuildArgs(string projectOrSolutionPath, string outputDir, ToolRunOptions options)
        {
            // The tool DLL path is the first argument to `dotnet`
            yield return _toolDllPath;
            yield return projectOrSolutionPath;
            yield return "--output";
            yield return outputDir;

            if (options.OutputFilename != null)
            {
                yield return "--filename";
                yield return options.OutputFilename;
            }

            if (options.OutputFormat != null)
            {
                yield return "--output-format";
                yield return options.OutputFormat;
            }

            if (options.ExcludeDev)
            {
                yield return "--exclude-dev";
            }

            if (options.ExcludeTestProjects)
            {
                yield return "--exclude-test-projects";
            }

            if (options.IncludeProjectReferences)
            {
                yield return "--include-project-references";
            }

            if (options.Recursive)
            {
                yield return "--recursive";
            }

            if (options.NoSerialNumber)
            {
                yield return "--no-serial-number";
            }

            if (options.DisableHashComputation)
            {
                yield return "--disable-hash-computation";
            }

            if (options.NuGetFeedUrl != null)
            {
                yield return "--url";
                yield return options.NuGetFeedUrl;
            }

            if (options.SetName != null)
            {
                yield return "--set-name";
                yield return options.SetName;
            }

            if (options.SetVersion != null)
            {
                yield return "--set-version";
                yield return options.SetVersion;
            }

            if (options.SetType != null)
            {
                yield return "--set-type";
                yield return options.SetType;
            }

            if (options.SpecVersion != null)
            {
                yield return "--spec-version";
                yield return options.SpecVersion;
            }

            if (options.ExcludeFilter != null)
            {
                yield return "--exclude-filter";
                yield return options.ExcludeFilter;
            }

            if (options.Framework != null)
            {
                yield return "--framework";
                yield return options.Framework;
            }

            if (options.AdditionalArgs != null)
            {
                yield return options.AdditionalArgs;
            }
        }
    }

    internal sealed class ToolRunOptions
    {
        public string OutputFilename { get; set; }
        /// <summary>auto, xml, json, unsafeJson</summary>
        public string OutputFormat { get; set; }
        public bool ExcludeDev { get; set; }
        public bool ExcludeTestProjects { get; set; }
        public bool IncludeProjectReferences { get; set; }
        public bool Recursive { get; set; }
        public bool NoSerialNumber { get; set; }
        public bool DisableHashComputation { get; set; }
        public string NuGetFeedUrl { get; set; }
        public string SetName { get; set; }
        public string SetVersion { get; set; }
        public string SetType { get; set; }
        public string SpecVersion { get; set; }
        public string ExcludeFilter { get; set; }
        public string Framework { get; set; }
        /// <summary>Appended verbatim to the command line for exotic scenarios.</summary>
        public string AdditionalArgs { get; set; }
    }

    internal sealed class ToolRunResult
    {
        public int ExitCode { get; }
        public string StdOut { get; }
        public string StdErr { get; }
        /// <summary>Full path to the generated BOM file, or null if none was found.</summary>
        public string OutputFilePath { get; }
        /// <summary>Raw text content of the BOM file, or null.</summary>
        public string BomContent { get; }

        public bool Success => ExitCode == 0;

        public ToolRunResult(int exitCode, string stdOut, string stdErr, string outputFilePath, string bomContent)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
            OutputFilePath = outputFilePath;
            BomContent = bomContent;
        }
    }
}
