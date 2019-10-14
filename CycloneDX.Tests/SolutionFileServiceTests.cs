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
using System.Threading.Tasks;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX.Services;

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
                    { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), new MockFileData(@"<Project></Project>") },
                });
            var solutionFileService = new SolutionFileService(mockFileSystem);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln"));
            
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
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), new MockFileData(@"<Project></Project>") },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), new MockFileData(@"<Project></Project>") },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), new MockFileData(@"<Project></Project>") },
                });
            var solutionFileService = new SolutionFileService(mockFileSystem);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln"));
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        [Fact]
        public async Task GetSolutionProjectReferences_ReturnsListOfProjects_IncludingSecondaryProjectReferences()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetProjectFileWithProjectReferences(
                        new string[] {
                            @"..\Project2\Project2.csproj",
                        })
                    },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), new MockFileData(@"<Project></Project>")}
                });
            var solutionFileService = new SolutionFileService(mockFileSystem);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.sln"));
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();
            
            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item));
        }
    }
}
