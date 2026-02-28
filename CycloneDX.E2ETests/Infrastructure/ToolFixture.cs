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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Publishes the CycloneDX tool once and exposes its path for use in tests.
    /// </summary>
    internal sealed class ToolFixture : IAsyncDisposable
    {
        private TempDirectory _publishDir;

        /// <summary>
        /// Full path to the published CycloneDX.dll.
        /// </summary>
        public string ToolDllPath { get; private set; }

        public async Task PublishAsync()
        {
            _publishDir = new TempDirectory();

            // Find the solution root — go up from the test assembly location
            var solutionRoot = FindSolutionRoot();
            var csprojPath = Path.Combine(solutionRoot, "CycloneDX", "CycloneDX.csproj");

            // Use whatever TFM matches the currently running runtime — it is guaranteed to be installed.
            var tfm = $"net{Environment.Version.Major}.{Environment.Version.Minor}";

            var result = await RunProcessAsync(
                "dotnet",
                $"publish \"{csprojPath}\" -c Release -f {tfm} -o \"{_publishDir.Path}\" /p:PackAsTool=false /nodeReuse:false",
                workingDir: solutionRoot
            ).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet publish failed (exit {result.ExitCode}):\n{result.StdErr}\n{result.StdOut}");
            }

            ToolDllPath = Path.Combine(_publishDir.Path, "CycloneDX.dll");
            if (!File.Exists(ToolDllPath))
                throw new FileNotFoundException($"Published tool not found at: {ToolDllPath}");
        }

        private static string FindSolutionRoot()
        {
            // Walk up from the current directory until we find CycloneDX.sln
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "CycloneDX.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("Could not find solution root (CycloneDX.sln).");
        }

        internal static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
            string executable,
            string arguments,
            string workingDir = null)
        {
            var psi = new ProcessStartInfo(executable, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory()
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            return (process.ExitCode, await stdoutTask, await stderrTask);
        }

        public ValueTask DisposeAsync()
        {
            _publishDir?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
