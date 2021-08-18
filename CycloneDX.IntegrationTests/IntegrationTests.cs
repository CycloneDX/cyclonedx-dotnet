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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Snapshooter;
using Snapshooter.Xunit;

namespace CycloneDX.IntegrationTests
{
    public class Tests
    {
        private string[] ArgsHelper(string path, string output, bool json, bool excludeDev)
        {
                var args = new List<string> {
                    path,
                    "--no-serial-number",
                    "--out", output
                };
                if (json) args.Add("--json");
                if (excludeDev) args.Add("--exclude-dev");
                return args.ToArray();
        }

        private async Task<int> CallCycloneDX(string path, string output, bool json, bool excludeDev)
        {
            var args = ArgsHelper(path, output, json, excludeDev);
            var exitCode = await Program.Main(args).ConfigureAwait(false);
            return exitCode;
        }

        private void AssertEqualIgnoringSpaces(string expected, string actual)
        {
            var exp = Regex.Replace(expected, @"\n\s*", "");
            var act = Regex.Replace(actual, @"\n\s*", "");
            Assert.Equal(exp, act);
        }

        [Theory]
        [InlineData("CSharp", false)]
        [InlineData("CSharp", true)]
        [InlineData("FullFrameworkAndCore", false)]
        [InlineData("FullFrameworkAndCore", true)]
        [InlineData("NoDependencies", false)]
        [InlineData("NoDependencies", true)]
        [InlineData("Vb", false)]
        [InlineData("Vb", true)]
        public async Task CallingCycloneDX_WithDirectoryPath_GeneratesBom(
            string directoryName, bool json)
        {
            // a lot of these are actually empty boms
            // .net core doesn't use packages.config
            using (var tempDir = new TempDirectory())
            {
                var exitCode = await CallCycloneDX(
                    Path.Join("Resources", directoryName),
                    tempDir.DirectoryPath,
                    json,
                    false);
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(
                    tempDir.DirectoryPath, json ? "bom.json" : "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(
                    directoryName, (json ? "Json" : "Xml")));
            }
        }

        [Theory]
        [InlineData("CSharp", false)]
        [InlineData("CSharp", true)]
        [InlineData("FullFrameworkAndCore", false)]
        [InlineData("FullFrameworkAndCore", true)]
        [InlineData("NoDependencies", false)]
        [InlineData("NoDependencies", true)]
        [InlineData("Vb", false)]
        [InlineData("Vb", true)]
        [InlineData("ProjectWithDevelopmentDependencies", false)]
        [InlineData("ProjectWithDevelopmentDependencies", true)]
        [InlineData("ProjectWithProjectReferences", false)]
        [InlineData("ProjectWithProjectReferences", true)]
        public async Task CallingCycloneDX_WithSolutionFilePath_GeneratesBom(
            string solutionName, bool json)
        {
            using (var tempDir = new TempDirectory())
            {
                var exitCode = await CallCycloneDX(
                    Path.Join("Resources", solutionName, $"{solutionName}.sln"),
                    tempDir.DirectoryPath,
                    json,
                    false);
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(
                    tempDir.DirectoryPath, json ? "bom.json" : "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(
                    solutionName, json ? "Json" : "Xml"));
            }
        }

        [Theory]
        [InlineData("CSharp", "CSharp", "csproj", false)]
        [InlineData("CSharp", "CSharp", "csproj", true)]
        [InlineData("FullFrameworkAndCore", "DotnetCore", "csproj", false)]
        [InlineData("FullFrameworkAndCore", "DotnetCore", "csproj", true)]
        [InlineData("FullFrameworkAndCore", "FullFramework", "csproj", false)]
        [InlineData("FullFrameworkAndCore", "FullFramework", "csproj", true)]
        [InlineData("NoDependencies", "NoDependencies", "csproj", false)]
        [InlineData("NoDependencies", "NoDependencies", "csproj", true)]
        [InlineData("NoDependencies", "NoDependencies.Tests", "csproj", false)]
        [InlineData("NoDependencies", "NoDependencies.Tests", "csproj", true)]
        [InlineData("Vb", "Vb", "vbproj", false)]
        [InlineData("Vb", "Vb", "vbproj", true)]
        [InlineData("ProjectWithDevelopmentDependencies", "ProjectWithDevelopmentDependencies", "csproj", false)]
        [InlineData("ProjectWithDevelopmentDependencies", "ProjectWithDevelopmentDependencies", "csproj", true)]
        [InlineData("ProjectWithProjectReferences", "ProjectWithProjectReferences", "csproj", false)]
        [InlineData("ProjectWithProjectReferences", "ProjectWithProjectReferences", "csproj", true)]
        public async Task CallingCycloneDX_WithProjectPath_GeneratesBom(
            string solutionName,
            string projectName,
            string projectFileExtension,
            bool json)
        {
            using (var tempDir = new TempDirectory())
            {
                var projectFilePath = Path.Join(
                        Path.Join("Resources", solutionName, projectName),
                        $"{projectName}.{projectFileExtension}");
                var exitCode = await CallCycloneDX(
                    projectFilePath,
                    tempDir.DirectoryPath,
                    json,
                    false);
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(
                    tempDir.DirectoryPath, json ? "bom.json" : "bom.xml"));

                Snapshot.Match(bomContents, SnapshotNameExtension.Create(
                    solutionName, projectName, projectFileExtension, 
                    json ? "Json" : "Xml"));
            }
        }

        [Fact]
        public async Task CallingCycloneDX_ExcludesDevelopmentDependencies()
        {
            using (var tempDir = new TempDirectory())
            {
                var exitCode = await CallCycloneDX(
                    Path.Join("Resources", "ProjectWithDevelopmentDependencies", "ProjectWithDevelopmentDependencies.sln"),
                    tempDir.DirectoryPath,
                    json: false,
                    excludeDev: true);
                // defensive assert, if this fails there is no point attempting to inspect the bom contents
                Assert.Equal(0, exitCode);

                var bomContents = File.ReadAllText(Path.Combine(
                    tempDir.DirectoryPath, "bom.xml"));

                Snapshot.Match(bomContents);
            }
        }
    }
}
