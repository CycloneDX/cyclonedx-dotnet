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
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public sealed class ProjectEvaluationServiceTests : IDisposable
    {
        private static readonly string[] PropertyNames = { "Version", "PackageId" };
        private static readonly string[] ItemTypes = { "PackageReference" };

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "cyclonedx-evaluation-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
                // best effort cleanup of the temporary directory
            }
        }

        // -----------------------------------------------------------------------
        // Mocked dotnet: command line, parsing and error handling
        // -----------------------------------------------------------------------

        private static string TargetResult(params object[] items)
            => JsonSerializer.Serialize(new { TargetResults = new { CycloneDXEvaluate = new { Result = "Success", Items = items } } });

        [Fact]
        public void EvaluateProjects_EvaluatesAllProjectsInOneDotnetProcess()
        {
            var project1 = XFS.Path(@"c:\P1\P1.csproj");
            var project2 = XFS.Path(@"c:\P2\P2.csproj");
            var arguments = new List<string>();
            var dotnet = new Mock<IDotnetCommandService>();
            dotnet
                .Setup(m => m.Run(It.IsAny<string>()))
                .Callback<string>(arguments.Add)
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = TargetResult(
                        new { Identity = project1, Version = "4.5.6", PackageId = "Contoso.P1", PackageReference = "Microsoft.NET.Test.Sdk;Moq" },
                        new { Identity = project2, Version = "", PackageId = "P2", PackageReference = "" })
                });

            var results = new ProjectEvaluationService(new MockFileSystem(), dotnet.Object)
                .EvaluateProjects(new[] { project1, project2 }, PropertyNames, ItemTypes, "net8.0", "Release");

            var argument = Assert.Single(arguments);
            Assert.StartsWith("msbuild ", argument, StringComparison.Ordinal);
            Assert.Contains("-getTargetResult:", argument, StringComparison.Ordinal);
            Assert.Contains("-p:TargetFramework=net8.0", argument, StringComparison.Ordinal);
            Assert.Contains("-p:Configuration=Release", argument, StringComparison.Ordinal);
            Assert.True(results[project1].Success);
            Assert.Equal("4.5.6", results[project1].Result.Properties["Version"]);
            Assert.Equal("Contoso.P1", results[project1].Result.Properties["PackageId"]);
            Assert.Equal(new[] { "Microsoft.NET.Test.Sdk", "Moq" }, results[project1].Result.Items["PackageReference"]);
            Assert.Equal("", results[project2].Result.Properties["Version"]);
            Assert.Empty(results[project2].Result.Items["PackageReference"]);
        }

        [Fact]
        public void EvaluateProjects_ProjectThatFailsToEvaluate_GetsItsErrorAndDoesNotAffectOthers()
        {
            var project1 = XFS.Path(@"c:\P1\P1.csproj");
            var project2 = XFS.Path(@"c:\P2\P2.csproj");
            var dotnet = new Mock<IDotnetCommandService>();
            dotnet
                .Setup(m => m.Run(It.IsAny<string>()))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 1,
                    StdOut = TargetResult(new { Identity = project1, Version = "4.5.6", PackageId = "P1", PackageReference = "" }),
                    StdErr = project2 + "(1,34): error MSB4019: The imported project \"missing.props\" was not found."
                });

            var results = new ProjectEvaluationService(new MockFileSystem(), dotnet.Object)
                .EvaluateProjects(new[] { project1, project2 }, PropertyNames, ItemTypes, null, null);

            Assert.True(results[project1].Success);
            Assert.False(results[project2].Success);
            Assert.Contains("MSB4019", results[project2].ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void EvaluateProjects_DotnetFails_EveryProjectGetsTheError()
        {
            var project1 = XFS.Path(@"c:\P1\P1.csproj");
            var project2 = XFS.Path(@"c:\P2\P2.csproj");
            var dotnet = new Mock<IDotnetCommandService>();
            dotnet
                .Setup(m => m.Run(It.IsAny<string>()))
                .Returns(new DotnetCommandResult { ExitCode = 1, StdOut = "MSBUILD : error MSB1001: Unknown switch.", StdErr = "" });

            var results = new ProjectEvaluationService(new MockFileSystem(), dotnet.Object)
                .EvaluateProjects(new[] { project1, project2 }, PropertyNames, ItemTypes, null, null);

            Assert.All(new[] { project1, project2 }, project =>
            {
                Assert.False(results[project].Success);
                Assert.Contains("MSB1001", results[project].ErrorMessage, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void EvaluateProjects_EscapesProjectPathsForMsbuild()
        {
            var project = XFS.Path(@"c:\100% R&D;$(Odd)\P.csproj");
            var fileSystem = new MockFileSystem();
            string generatedProject = null;
            var dotnet = new Mock<IDotnetCommandService>();
            dotnet
                .Setup(m => m.Run(It.IsAny<string>()))
                .Callback<string>(arguments => generatedProject = fileSystem.File.ReadAllText(arguments.Split('"')[1]))
                .Returns(new DotnetCommandResult { ExitCode = 0, StdOut = TargetResult() });

            new ProjectEvaluationService(fileSystem, dotnet.Object).EvaluateProjects(new[] { project }, PropertyNames, ItemTypes, null, null);

            Assert.Contains("100%25 R&amp;D%3B%24(Odd)", generatedProject, StringComparison.Ordinal);
            Assert.Empty(fileSystem.AllFiles);
        }

        // -----------------------------------------------------------------------
        // Real dotnet: projects in a temporary directory
        // -----------------------------------------------------------------------

        // The Directory.Build.props also stops MSBuild from picking one up from a parent directory
        private string WriteProject(string name, string csproj, string directoryBuildProps = "<Project />")
        {
            var projectDirectory = Path.Combine(_directory, name);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(_directory, "Directory.Build.props"), directoryBuildProps);
            var projectFilePath = Path.Combine(projectDirectory, name + ".csproj");
            File.WriteAllText(projectFilePath, csproj);
            return projectFilePath;
        }

        private static Dictionary<string, DotnetUtilsResult<EvaluatedProject>> Evaluate(IEnumerable<string> projects, string framework = null, string configuration = null)
            => new ProjectEvaluationService(new FileSystem(), new DotnetCommandService())
                .EvaluateProjects(projects, new[] { "Version", "PackageId", "IsTestProject" }, ItemTypes, framework, configuration);

        [Fact]
        public void EvaluateProjects_Dotnet_SeesValuesAndItemsFromDirectoryBuildProps()
        {
            var project = WriteProject(
                "App.Tests",
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>",
                "<Project><PropertyGroup><Version>4.5.6</Version><AssemblyName>Contoso.$(MSBuildProjectName)</AssemblyName></PropertyGroup>" +
                "<ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.11.1\" /></ItemGroup></Project>");

            var result = Evaluate(new[] { project })[project];

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("4.5.6", result.Result.Properties["Version"]);
            Assert.Equal("Contoso.App.Tests", result.Result.Properties["PackageId"]);
            Assert.Contains("Microsoft.NET.Test.Sdk", result.Result.Items["PackageReference"]);
        }

        [Theory]
        [InlineData(null, null, "1.0.0")]
        [InlineData(null, "Release", "2.0.0")]
        [InlineData("net9.0", null, "9.0.0")]
        public void EvaluateProjects_Dotnet_AppliesFrameworkAndConfiguration(string framework, string configuration, string expectedVersion)
        {
            var project = WriteProject(
                "App",
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup></Project>",
                "<Project>" +
                "<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\"><Version>2.0.0</Version></PropertyGroup>" +
                "<PropertyGroup Condition=\"'$(TargetFramework)' == 'net9.0'\"><Version>9.0.0</Version></PropertyGroup>" +
                "</Project>");

            var result = Evaluate(new[] { project }, framework, configuration)[project];

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(expectedVersion, result.Result.Properties["Version"]);
        }

        [Fact]
        public void EvaluateProjects_Dotnet_ProjectPathWithSpecialCharacters()
        {
            var project = WriteProject("100% R&D;$(Odd)'s @Lib", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><Version>7.8.9</Version></PropertyGroup></Project>");

            var result = Evaluate(new[] { project })[project];

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("7.8.9", result.Result.Properties["Version"]);
        }

        [Fact]
        public void EvaluateProjects_Dotnet_BrokenProjectDoesNotPreventTheOthers()
        {
            var good = WriteProject("Good", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><Version>3.2.1</Version></PropertyGroup></Project>");
            var broken = WriteProject("Broken", "<Project Sdk=\"Microsoft.NET.Sdk\"><Import Project=\"missing.props\" /></Project>");
            Directory.CreateDirectory(Path.Combine(_directory, "NoProject"));
            var notXml = WriteProject("NotXml", "this is not a project file");
            var paths = new[] { good, broken, Path.Combine(_directory, "NoProject"), notXml, Path.Combine(_directory, "Missing", "Missing.csproj") };

            var results = Evaluate(paths);

            Assert.True(results[good].Success, results[good].ErrorMessage);
            Assert.Equal("3.2.1", results[good].Result.Properties["Version"]);
            Assert.Contains("MSB4019", results[broken].ErrorMessage, StringComparison.Ordinal);
            Assert.StartsWith("error MSB3202", results[paths[4]].ErrorMessage, StringComparison.Ordinal);
            Assert.All(paths.Skip(1), path => Assert.False(results[path].Success, path));
        }
    }
}
