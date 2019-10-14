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

using System.Collections.Generic;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void CallingCycloneDX_WithoutSolutionFile_ReturnsSolutionOrProjectFileParameterMissingExitCode()
        {
            var exitCode = Program.Main(new string[] {});

            Assert.Equal((int)ExitCode.SolutionOrProjectFileParameterMissing, exitCode);
        }

        [Fact]
        public void CallingCycloneDX_WithoutOutputDirectory_ReturnsOutputDirectoryParameterMissingExitCode()
        {
            var exitCode = Program.Main(new string[] { XFS.Path(@"c:\SolutionPath\Solution.sln") });

            Assert.Equal((int)ExitCode.OutputDirectoryParameterMissing, exitCode);
        }

        [Fact]
        public void CallingCycloneDX_CreatesOutputDirectory()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project\Project.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), Helpers.GetProjectFileWithPackageReference("Package", "1.2.3")},
                });
            var mockHttpClient = Helpers.GetNugetMockHttpClient(new List<NugetPackage>
            {
                new NugetPackage { Name = "Package", Version = "1.2.3" },
            });
            Program.fileSystem = mockFileSystem;
            Program.httpClient = mockHttpClient;
            var args = new string[]
            {
                XFS.Path(@"c:\SolutionPath\SolutionFile.sln"),
                "-o", XFS.Path(@"c:\NewDirectory")
            };

            var exitCode = Program.Main(args);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\NewDirectory\bom.xml")));
        }

        [Fact]
        public void CallingCycloneDX_CreatesBomFromSolutionFile()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project2\Project2.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project3\Project3.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithPackageReference("Package1", "1.2.3")},
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetProjectFileWithPackageReference("Package2", "1.2.3")},
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), Helpers.GetProjectFileWithPackageReference("Package3", "1.2.3")},
                });
            var mockHttpClient = Helpers.GetNugetMockHttpClient(new List<NugetPackage>
            {
                new NugetPackage { Name = "Package1", Version = "1.2.3" },
                new NugetPackage { Name = "Package2", Version = "1.2.3" },
                new NugetPackage { Name = "Package3", Version = "1.2.3" },
            });
            Program.fileSystem = mockFileSystem;
            Program.httpClient = mockHttpClient;
            var args = new string[]
            {
                XFS.Path(@"c:\SolutionPath\SolutionFile.sln"),
                "-o", XFS.Path(@"c:\SolutionPath")
            };

            var exitCode = Program.Main(args);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\SolutionPath\bom.xml")));
        }

        [Fact]
        public void CallingCycloneDX_CreatesBomFromDirectory()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project1\packages.config"), Helpers.GetPackagesFileWithPackageReference("Package1", "1.2.3") },
                    { XFS.Path(@"c:\SolutionPath\Project2\packages.config"), Helpers.GetPackagesFileWithPackageReference("Package2", "1.2.3") },
                    { XFS.Path(@"c:\SolutionPath\Project3\packages.config"), Helpers.GetPackagesFileWithPackageReference("Package3", "1.2.3") },
                });
            var mockHttpClient = Helpers.GetNugetMockHttpClient(new List<NugetPackage>
            {
                new NugetPackage { Name = "Package1", Version = "1.2.3" },
                new NugetPackage { Name = "Package2", Version = "1.2.3" },
                new NugetPackage { Name = "Package3", Version = "1.2.3" },
            });
            Program.fileSystem = mockFileSystem;
            Program.httpClient = mockHttpClient;
            var args = new string[]
            {
                XFS.Path(@"c:\SolutionPath"),
                "-o", XFS.Path(@"c:\SolutionPath")
            };

            var exitCode = Program.Main(args);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\SolutionPath\bom.xml")));
        }

        [Fact]
        public void CallingCycloneDX_CreatesBomFromPackagesFile()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\Project\packages.config"), Helpers.GetPackagesFileWithPackageReferences(
                        new List<NugetPackage> {
                            new NugetPackage { Name = "Package1", Version = "1.2.3" },
                            new NugetPackage { Name = "Package2", Version = "1.2.3" },
                            new NugetPackage { Name = "Package3", Version = "1.2.3" },
                        })
                    },
                });
            var mockHttpClient = Helpers.GetNugetMockHttpClient(new List<NugetPackage>
            {
                new NugetPackage { Name = "Package1", Version = "1.2.3" },
                new NugetPackage { Name = "Package2", Version = "1.2.3" },
                new NugetPackage { Name = "Package3", Version = "1.2.3" },
            });
            Program.fileSystem = mockFileSystem;
            Program.httpClient = mockHttpClient;
            var args = new string[]
            {
                XFS.Path(@"c:\SolutionPath\Project\packages.config"),
                "-o", XFS.Path(@"c:\SolutionPath")
            };

            var exitCode = Program.Main(args);

            Assert.Equal((int)ExitCode.OK, exitCode);
            Assert.True(mockFileSystem.FileExists(XFS.Path(@"c:\SolutionPath\bom.xml")));
        }
    }
}
