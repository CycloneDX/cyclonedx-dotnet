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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Snapshooter;
using Snapshooter.Xunit;

namespace CycloneDX.IntegrationTests
{
    public class Tests
    {
        private void AssertEqualIgnoringSpaces(string expected, string actual)
        {
            var exp = Regex.Replace(expected, @"\n\s*", "");
            var act = Regex.Replace(actual, @"\n\s*", "");
            Assert.Equal(exp, act);
        }

        [Theory]
        [InlineData("CSharp")]
        [InlineData("FullFrameworkAndCore")]
        [InlineData("NoDependencies")]
        [InlineData("Vb")]
        public async Task CallingCycloneDX_WithDirectoryPath_GeneratesBom(string directoryName)
        {
            // a lot of these are actually empty boms
            // .net core doesn't use packages.config
            using (var tempDir = new TempDirectory())
            {
                var exitCode = await Program.Main(new string[] {
                    Path.Join("Resources", directoryName),
                    "--noSerialNumber",
                    "--out", tempDir.DirectoryPath
                });
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(tempDir.DirectoryPath, "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(directoryName));
            }
        }

        [Theory]
        [InlineData("CSharp")]
        [InlineData("FullFrameworkAndCore")]
        [InlineData("NoDependencies")]
        [InlineData("Vb")]
        public async Task CallingCycloneDX_WithSolutionFilePath_GeneratesBom(string solutionName)
        {
            using (var tempDir = new TempDirectory())
            {
                var exitCode = await Program.Main(new string[] {
                    Path.Join("Resources", solutionName, $"{solutionName}.sln"),
                    "--noSerialNumber",
                    "--out", tempDir.DirectoryPath
                });
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(tempDir.DirectoryPath, "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(solutionName));
            }
        }

        [Theory]
        [InlineData("CSharp", "CSharp", "csproj")]
        [InlineData("FullFrameworkAndCore", "DotnetCore", "csproj")]
        [InlineData("FullFrameworkAndCore", "FullFramework", "csproj")]
        [InlineData("NoDependencies", "NoDependencies", "csproj")]
        [InlineData("NoDependencies", "NoDependencies.Tests", "csproj")]
        [InlineData("Vb", "Vb", "vbproj")]
        public async Task CallingCycloneDX_WithProjectPath_GeneratesBom(string solutionName, string projectName, string projectFileExtension)
        {
            using (var tempDir = new TempDirectory())
            {
                var projectFilePath = Path.Join(
                        Path.Join("Resources", solutionName, projectName),
                        $"{projectName}.{projectFileExtension}");
                var exitCode = await Program.Main(new string[] {
                    projectFilePath,
                    "--noSerialNumber",
                    "--out", tempDir.DirectoryPath
                });
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(tempDir.DirectoryPath, "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(solutionName, projectName, projectFileExtension));
            }
        }
    }
}
