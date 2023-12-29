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
using System.Threading.Tasks;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using CycloneDX.Interfaces;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Moq;
using CycloneDX.Services;
using CycloneDX.Models;
using System.IO;

namespace CycloneDX.Tests
{
    public class SolutionFileServiceTests
    {
        [Fact]
        public async Task GetSolutionProjectReferences_ReturnsProjectThatExists()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project\Project.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), Helpers.GetEmptyProjectFile() },
                });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .Setup(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln")).ConfigureAwait(true);
            
            Assert.Collection(projects, 
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), item));
        }

        [Fact]
        public async Task GetSolutionProjectReferences_ReturnsListOfProjects()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project2\Project2.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project3\Project3.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), Helpers.GetEmptyProjectFile() },
                });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .SetupSequence(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        [Fact]
        public async Task GetSolutionProjectReferences_ReturnsListOfProjectsIncludingFSharpAndVB()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project2\Project2.fsproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project3\Project3.vbproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.fsproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.vbproj"), Helpers.GetEmptyProjectFile() },
                });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .SetupSequence(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.fsproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.vbproj"), item));
        }

        [Fact]
        public async Task GetSolutionProjectReferences_ReturnsListOfProjects_IncludingSecondaryProjectReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), Helpers.GetEmptyProjectFile() }
                });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .SetupSequence(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>
                    { new DotnetDependency() {
                        Name = "Project2",
                        Version = "1.0.0",
                        Path = XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"),

                    } })
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item));
        }
    }
}
