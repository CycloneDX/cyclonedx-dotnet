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

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using DotnetCommandResult = CycloneDX.Core.Models.DotnetCommandResult;

namespace CycloneDX.Services
{
    public class DotnetCommandService : IDotnetCommandService
    {
        public int TimeoutMilliseconds { get; set; } = 300000;

        public DotnetCommandResult Run(string arguments)
        {
            return Run(Directory.GetCurrentDirectory(), arguments);
        }

        public DotnetCommandResult Run(string workingDirectory, string arguments)
        {
            Contract.Requires(arguments != null);
            var psi = new ProcessStartInfo(DotNetExe.FullPathOrDefault(), arguments)
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            
            using (var p = Process.Start(psi))
            {
                var output = new StringBuilder();
                var errors = new StringBuilder();
                var outputTask = ConsumeStreamReaderAsync(p.StandardOutput, output);
                var errorTask = ConsumeStreamReaderAsync(p.StandardError, errors);

                var processExited = p.WaitForExit(TimeoutMilliseconds);

                if (processExited)
                {
                    Task.WaitAll(outputTask, errorTask);

                    return new DotnetCommandResult
                    {
                        ExitCode = p.ExitCode,
                        StdOut = output.ToString(),
                        StdErr = errors.ToString()
                    };
                }

                p.Kill();
                return new DotnetCommandResult
                {
                    ExitCode = -1,
                    StdOut = arguments.StartsWith("restore ", System.StringComparison.InvariantCulture) ?
                        $"Timeout running dotnet restore, try running \"dotnet restore\" before \"dotnet CycloneDX\""
                        : $"Timeout running dotnet {arguments}"
                };
            }
        }
        
        private static async Task ConsumeStreamReaderAsync(StreamReader reader, StringBuilder lines)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                lines.AppendLine(line);
            }
        }
    }
}