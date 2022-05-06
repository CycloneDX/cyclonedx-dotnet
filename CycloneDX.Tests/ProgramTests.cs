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

using System.Collections.Generic;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Moq;
using CycloneDX.Models;
using CycloneDX.Services;
using System.IO;
using Microsoft.DotNet.PlatformAbstractions;

namespace CycloneDX.Tests
{
    public class ProgramTests
    {
        [Fact]
        public async Task CallingCycloneDX_WithoutSolutionFile_ReturnsSolutionOrProjectFileParameterMissingExitCode()
        {
            var exitCode = await Program.Main(new string[] {}).ConfigureAwait(false);

            Assert.Equal((int)ExitCode.SolutionOrProjectFileParameterMissing, exitCode);
        }

        [Fact]
        public async Task CallingCycloneDX_WithoutOutputDirectory_ReturnsOutputDirectoryParameterMissingExitCode()
        {
            var exitCode = await Program.Main(new string[] { XFS.Path(@"c:\SolutionPath\Solution.sln") }).ConfigureAwait(false);

            Assert.Equal((int)ExitCode.OutputDirectoryParameterMissing, exitCode);
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
                .Setup(s => s.GetSolutionNugetPackages(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new HashSet<NugetPackage>());
            Program.fileSystem = mockFileSystem;
            Program.solutionFileService = mockSolutionFileService.Object;
            var args = new string[]
            {
                XFS.Path(@"c:\SolutionPath\SolutionFile.sln"),
                "-o", XFS.Path(@"c:\NewDirectory")
            };

            var exitCode = await Program.Main(args).ConfigureAwait(false);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\NewDirectory\bom.xml")));
        }

        [Fact]
        public void CheckMetaDataTemplate()
        {
            var bom = new Bom();
            string resourcePath = Path.Join(ApplicationEnvironment.ApplicationBasePath, "Resources", "metadata");
            bom = Program.ReadMetaDataFromFile(bom, Path.Join(resourcePath, "cycloneDX-metadata-template.xml"));
            Assert.NotNull(bom.Metadata);
            Assert.Matches("CycloneDX", bom.Metadata.Component.Name);
            Assert.NotEmpty(bom.Metadata.Tools);
            Assert.Matches("CycloneDX", bom.Metadata.Tools[0].Vendor);
            Assert.Matches("1.2.0", bom.Metadata.Tools[0].Version);
        }
    }
}
