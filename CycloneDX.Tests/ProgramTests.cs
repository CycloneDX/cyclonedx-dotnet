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
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using Moq;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class ResolveCredentialTests
    {
        [Fact]
        public void WarnIfCredentialPassedAsCLIArg_WritesToStderr_WhenValueProvided()
        {
            var originalErr = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            try
            {
                Program.WarnIfCredentialPassedAsCLIArg("secret", "--some-flag", "SOME_ENV_VAR");
                Assert.Contains("WARNING", sw.ToString());
                Assert.Contains("--some-flag", sw.ToString());
                Assert.Contains("SOME_ENV_VAR", sw.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }

        [Fact]
        public void WarnIfCredentialPassedAsCLIArg_WritesNothing_WhenValueIsNull()
        {
            var originalErr = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            try
            {
                Program.WarnIfCredentialPassedAsCLIArg(null, "--some-flag", "SOME_ENV_VAR");
                Assert.Empty(sw.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }

        [Fact]
        public void WarnIfCredentialPassedAsCLIArg_WritesNothing_WhenValueIsEmpty()
        {
            var originalErr = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            try
            {
                Program.WarnIfCredentialPassedAsCLIArg(string.Empty, "--some-flag", "SOME_ENV_VAR");
                Assert.Empty(sw.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        }

        [Fact]
        public void ResolveCredential_ReturnsCliValue_WhenProvided()
        {
            var result = Program.ResolveCredential("cli-value", "SOME_ENV_VAR");
            Assert.Equal("cli-value", result);
        }

        [Fact]
        public void ResolveCredential_ReturnsEnvVar_WhenCliValueIsNull()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_1", "env-value");
            try
            {
                var result = Program.ResolveCredential(null, "CYCLONEDX_TEST_CRED_1");
                Assert.Equal("env-value", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_1", null);
            }
        }

        [Fact]
        public void ResolveCredential_ReturnsEnvVar_WhenCliValueIsEmpty()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_2", "env-value");
            try
            {
                var result = Program.ResolveCredential(string.Empty, "CYCLONEDX_TEST_CRED_2");
                Assert.Equal("env-value", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_2", null);
            }
        }

        [Fact]
        public void ResolveCredential_PrefersCliValue_OverEnvVar()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_3", "env-value");
            try
            {
                var result = Program.ResolveCredential("cli-value", "CYCLONEDX_TEST_CRED_3");
                Assert.Equal("cli-value", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_3", null);
            }
        }

        [Fact]
        public void ResolveCredential_ReturnsNull_WhenNeitherCliNorEnvVarSet()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_4", null);
            var result = Program.ResolveCredential(null, "CYCLONEDX_TEST_CRED_4");
            Assert.Null(result);
        }

        [Fact]
        public void ResolveCredential_FallsBackToSecondEnvVar_WhenFirstIsAbsent()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_5A", null);
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_5B", "fallback-value");
            try
            {
                var result = Program.ResolveCredential(null, "CYCLONEDX_TEST_CRED_5A", "CYCLONEDX_TEST_CRED_5B");
                Assert.Equal("fallback-value", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_5B", null);
            }
        }

        [Fact]
        public void ResolveCredential_PrefersFirstEnvVar_OverSecond()
        {
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_6A", "primary-value");
            Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_6B", "secondary-value");
            try
            {
                var result = Program.ResolveCredential(null, "CYCLONEDX_TEST_CRED_6A", "CYCLONEDX_TEST_CRED_6B");
                Assert.Equal("primary-value", result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_6A", null);
                Environment.SetEnvironmentVariable("CYCLONEDX_TEST_CRED_6B", null);
            }
        }
    }

    public class ProgramTests
    {

        [Fact]
        public async Task CallingCycloneDX_CreatesOutputDirectory()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), "" }
                });
            var mockSolutionFileService = new Mock<ISolutionFileService>();
            mockSolutionFileService
                .Setup(s => s.GetSolutionDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());

            Runner runner = new Runner(fileSystem: mockFileSystem, null, null, null, null, null, solutionFileService: mockSolutionFileService.Object, null);

            RunOptions runOptions = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path(@"c:\SolutionPath\SolutionFile.sln"),
                outputDirectory = XFS.Path(@"c:\NewDirectory")
            };
            var exitCode = await runner.HandleCommandAsync(runOptions);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\NewDirectory\bom.xml")));
        }

        [Fact]
        public async Task CallingCycloneDX_WithOutputFilename_CreatesOutputFilename()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), "" }
                });
            var mockSolutionFileService = new Mock<ISolutionFileService>();
            mockSolutionFileService
                .Setup(s => s.GetSolutionDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());

            Runner runner = new Runner(fileSystem: mockFileSystem, null, null, null, null, null, solutionFileService: mockSolutionFileService.Object, null);            

            RunOptions runOptions = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path(@"c:\SolutionPath\SolutionFile.sln"),
                outputDirectory = XFS.Path(@"c:\NewDirectory"),
                outputFilename = XFS.Path(@"my_bom.xml")
            };

            var exitCode = await runner.HandleCommandAsync(runOptions);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\NewDirectory\my_bom.xml")));
        }

        [Fact]
        public void CheckMetaDataTemplate()
        {
            var bom = new Bom();
            string resourcePath = Path.Join(AppContext.BaseDirectory, "Resources", "metadata");
            bom = Runner.ReadMetaDataFromFile(bom, Path.Join(resourcePath, "cycloneDX-metadata-template.xml"), new FileSystem());
            Assert.NotNull(bom.Metadata);
            Assert.Matches("CycloneDX", bom.Metadata.Component.Name);
            Assert.NotEmpty(bom.Metadata.Tools.Tools);
            Assert.Matches("CycloneDX", bom.Metadata.Tools.Tools[0].Vendor);
            Assert.Matches("1.2.0", bom.Metadata.Tools.Tools[0].Version);
        }

        [Fact]
        public void AddMetadataTool_AddsToolAsComponent()
        {
            var bom = new Bom();
            Runner.AddMetadataTool(bom);
            Assert.NotNull(bom.Metadata.Tools.Components);
            Assert.Single(bom.Metadata.Tools.Components);
            var component = bom.Metadata.Tools.Components[0];
            Assert.Equal(Component.Classification.Application, component.Type);
            Assert.Equal("CycloneDX module for .NET", component.Name);
            Assert.Single(component.Authors);
            Assert.Equal("CycloneDX", component.Authors[0].Name);
            Assert.NotNull(component.Version);
        }

        [Fact]
        public void AddMetadataTool_CalledTwice_UpdatesVersionNotDuplicates()
        {
            var bom = new Bom();
            Runner.AddMetadataTool(bom);
            Runner.AddMetadataTool(bom);
            Assert.Single(bom.Metadata.Tools.Components);
        }

        [Theory]
        [InlineData(@"c:\SolutionPath\SolutionFile.sln", false)]
        [InlineData(@"c:\SolutionPath\ProjectFile.csproj", false)]
        [InlineData(@"c:\SolutionPath\ProjectFile.csproj", true)]
        [InlineData(@"c:\SolutionPath\packages.config", false)]
        public async Task CallingCycloneDX_WithSolutionOrProjectFileThatDoesntExistsReturnAnythingButZero(string path, bool rs)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
            var mockSolutionFileService = new Mock<ISolutionFileService>();
            mockSolutionFileService
                .Setup(s => s.GetSolutionDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());

            Runner runner = new Runner(fileSystem: mockFileSystem, null, null, null, null, null, solutionFileService: mockSolutionFileService.Object, null);

            RunOptions runOptions = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path(path),
                scanProjectReferences = rs,
                outputDirectory = XFS.Path(@"c:\NewDirectory"),
                outputFilename = XFS.Path(@"my_bom.xml")
            };

            var exitCode = await runner.HandleCommandAsync(runOptions);

            Assert.NotEqual((int)ExitCode.OK, exitCode);            
        }
    }
}
