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
using System.Text;
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

            var args = BuildArgs(projectOrSolutionPath, outputDir, options);

            var (exitCode, stdOut, stdErr) = await RunProcessAsync(
                "dotnet",
                $"\"{_toolDllPath}\" {args}",
                workingDir: Path.GetDirectoryName(projectOrSolutionPath)
            ).ConfigureAwait(false);

            // Find the generated BOM file
            string outputFilePath = null;
            string bomContent = null;

            if (exitCode == 0)
            {
                var filename = options.OutputFilename;
                if (filename == null)
                {
                    // Auto-detect: tool defaults to bom.xml or bom.json
                    var xmlPath = Path.Combine(outputDir, "bom.xml");
                    var jsonPath = Path.Combine(outputDir, "bom.json");
                    if (File.Exists(xmlPath)) { outputFilePath = xmlPath; }
                    else if (File.Exists(jsonPath)) { outputFilePath = jsonPath; }
                }
                else
                {
                    outputFilePath = Path.Combine(outputDir, filename);
                }

                if (outputFilePath != null && File.Exists(outputFilePath))
                    bomContent = await File.ReadAllTextAsync(outputFilePath).ConfigureAwait(false);
            }

            return new ToolRunResult(exitCode, stdOut, stdErr, outputFilePath, bomContent);
        }

        private static string BuildArgs(string projectOrSolutionPath, string outputDir, ToolRunOptions options)
        {
            var sb = new StringBuilder();
            sb.Append($"\"{projectOrSolutionPath}\"");
            sb.Append($" --output \"{outputDir}\"");

            if (options.OutputFilename != null)
            {
                sb.Append($" --filename \"{options.OutputFilename}\"");
            }

            if (options.OutputFormat != null)
            {
                sb.Append($" --output-format {options.OutputFormat}");
            }

            if (options.ExcludeDev)
            {
                sb.Append(" --exclude-dev");
            }

            if (options.ExcludeTestProjects)
            {
                sb.Append(" --exclude-test-projects");
            }

            if (options.IncludeProjectReferences)
            {
                sb.Append(" --include-project-references");
            }

            if (options.Recursive)
            {
                sb.Append(" --recursive");
            }

            if (options.NoSerialNumber)
            {
                sb.Append(" --no-serial-number");
            }

            if (options.DisableHashComputation)
            {
                sb.Append(" --disable-hash-computation");
            }

            if (options.NuGetFeedUrl != null)
            {
                sb.Append($" --url \"{options.NuGetFeedUrl}\"");
            }

            if (options.SetName != null)
            {
                sb.Append($" --set-name \"{options.SetName}\"");
            }

            if (options.SetVersion != null)
            {
                sb.Append($" --set-version \"{options.SetVersion}\"");
            }

            if (options.SetType != null)
            {
                sb.Append($" --set-type \"{options.SetType}\"");
            }

            if (options.SpecVersion != null)
            {
                sb.Append($" --spec-version {options.SpecVersion}");
            }

            if (options.ExcludeFilter != null)
            {
                sb.Append($" --exclude-filter \"{options.ExcludeFilter}\"");
            }

            if (options.Framework != null)
            {
                sb.Append($" --framework {options.Framework}");
            }

            if (options.AdditionalArgs != null)
            {
                sb.Append($" {options.AdditionalArgs}");
            }

            return sb.ToString();
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
