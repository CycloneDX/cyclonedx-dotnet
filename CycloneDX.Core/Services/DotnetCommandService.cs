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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using DotnetCommandResult = CycloneDX.Models.DotnetCommandResult;

namespace CycloneDX.Services
{
    public class DotnetCommandService : IDotnetCommandService
    {
        public DotnetCommandService() {}
        
        public DotnetCommandResult Run(string arguments)
        {
            return Run(Directory.GetCurrentDirectory(), arguments);
        }

        public DotnetCommandResult Run(string workingDirectory, string arguments)
        {
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
                var exitCode = 0;
                var output = new StringBuilder();
                var errors = new StringBuilder();
                var outputTask = ConsumeStreamReaderAsync(p.StandardOutput, output);
                var errorTask = ConsumeStreamReaderAsync(p.StandardError, errors);

                var processExited = p.WaitForExit(60000);

                if (processExited)
                {
                    exitCode = p.ExitCode;
                }
                else
                {
                    p.Kill();
                    exitCode = -1;
                }

                Task.WaitAll(outputTask, errorTask);

                var result = new DotnetCommandResult
                {
                    ExitCode = exitCode,
                    StdOut = output.ToString(),
                    StdErr = errors.ToString()
                };

                return result;
            }
        }
        
        private async Task ConsumeStreamReaderAsync(StreamReader reader, StringBuilder lines)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lines.AppendLine(line);
            }
        }
    }
}