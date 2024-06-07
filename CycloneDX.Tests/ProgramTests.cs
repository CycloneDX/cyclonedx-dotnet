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
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using Moq;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class ProgramTests
    {
        [Fact]
        public async Task CallingCycloneDX_WithoutSolutionFile_ReturnsInvalidOptions()
        {
            var exitCode = await Program.Main(new string[] { }).ConfigureAwait(true);

            Assert.Equal((int)ExitCode.InvalidOptions, exitCode);
        }

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
            bom = Runner.ReadMetaDataFromFile(bom, Path.Join(resourcePath, "cycloneDX-metadata-template.xml"));
            Assert.NotNull(bom.Metadata);
            Assert.Matches("CycloneDX", bom.Metadata.Component.Name);
            Assert.NotEmpty(bom.Metadata.Tools.Tools);
            Assert.Matches("CycloneDX", bom.Metadata.Tools.Tools[0].Vendor);
            Assert.Matches("1.2.0", bom.Metadata.Tools.Tools[0].Version);
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
