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
using System.Linq;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class SolutionFileServiceTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task MixedSdkLegacyMerge_LegacyRootsAreExplicitAndTestScopeIsPreserved(bool reverse, bool isLegacyTestProject)
        {
            var sdkProject = "<Project Path=\"sdk/sdk.csproj\" />";
            var legacyProject = "<Project Path=\"legacy/legacy.csproj\" />";
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                ["/solution/test.slnx"] = $"<Solution>\n{(reverse ? legacyProject : sdkProject)}\n{(reverse ? sdkProject : legacyProject)}\n</Solution>",
                ["/solution/sdk/sdk.csproj"] = Helpers.GetEmptyProjectFile(),
                ["/solution/sdk/obj/project.assets.json"] = "{}",
                ["/solution/legacy/legacy.csproj"] = $"<Project><PropertyGroup><IsTestProject>{isLegacyTestProject.ToString().ToLowerInvariant()}</IsTestProject></PropertyGroup></Project>",
                ["/solution/legacy/packages.config"] = "<packages><package id=\"Shared\" version=\"1.0.0\" /><package id=\"LegacyOnly\" version=\"1.0.0\" /><package id=\"LegacyDev\" version=\"1.0.0\" developmentDependency=\"true\" /></packages>"
            });
            var utils = new Mock<IDotnetUtilsService>();
            utils.Setup(s => s.GetAssetsPath(It.IsAny<string>())).Returns(new DotnetUtilsResult<string> { Result = "" });
            var assets = new Mock<IProjectAssetsFileService>();
            assets.Setup(s => s.GetDotnetDependencys(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(() => new HashSet<DotnetDependency>());
            var sdkProjectPath = fileSystem.Path.GetFullPath("/solution/sdk/sdk.csproj");
            assets.Setup(s => s.GetDotnetDependencys(sdkProjectPath, It.IsAny<string>(), false)).Returns(() => new HashSet<DotnetDependency>
            {
                new() { Name = "SdkRoot", Version = "1.0.0", IsDirectReference = true, IsNonDevelopmentReachable = true, IsRuntimeReachable = true, Scope = Component.ComponentScope.Required },
                new() { Name = "Shared", Version = "1.0.0", IsDevDependency = true, IsNonDevelopmentReachable = false, IsRuntimeReachable = false, Scope = Component.ComponentScope.Excluded }
            });
            var projectService = new ProjectFileService(fileSystem, utils.Object, new PackagesFileService(fileSystem), assets.Object) { DisablePackageRestore = true };
            var packages = await new SolutionFileService(fileSystem, projectService)
                .GetSolutionDotnetDependencys("/solution/test.slnx", null, false, null, null).ConfigureAwait(true);
            assets.Verify(s => s.GetDotnetDependencys(sdkProjectPath, It.IsAny<string>(), false), Times.Once);
            Assert.Contains(packages, p => p.Name == "SdkRoot");
            foreach (var name in new[] { "Shared", "LegacyOnly" })
            {
                var package = packages.Single(p => p.Name == name);
                Assert.True(package.IsNonDevelopmentReachable);
                Assert.Equal(!isLegacyTestProject, package.IsRuntimeReachable);
                Assert.Equal(isLegacyTestProject ? Component.ComponentScope.Excluded : Component.ComponentScope.Required, package.Scope);
            }
            var legacyDev = packages.Single(p => p.Name == "LegacyDev");
            Assert.False(legacyDev.IsNonDevelopmentReachable);
            Assert.False(legacyDev.IsRuntimeReachable);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SolutionMerge_RuntimeUseAndDirectFlagsAreOrderIndependent(bool reverse)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                ["/solution/test.slnx"] = "<Solution>\n<Project Path=\"first.csproj\" />\n<Project Path=\"second.csproj\" />\n</Solution>",
                ["/solution/first.csproj"] = Helpers.GetEmptyProjectFile(),
                ["/solution/second.csproj"] = Helpers.GetEmptyProjectFile()
            });
            var dev = new HashSet<DotnetDependency>
            {
                new() { Name = "Shared", Version = "1.0.0", IsDevDependency = true, IsDirectReference = true,
                    IsRuntimeReachable = false, Scope = Component.ComponentScope.Excluded,
                    Dependencies = new Dictionary<string, string> { ["Child"] = "1.0.0" } }
            };
            var runtime = new HashSet<DotnetDependency>
            {
                new() { Name = "Shared", Version = "1.0.0", IsRuntimeReachable = true, Scope = Component.ComponentScope.Required,
                    Dependencies = new Dictionary<string, string> { ["Child"] = "2.0.0" } }
            };
            var projectService = new Mock<IProjectFileService>();
            projectService.Setup(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>())).ReturnsAsync(new HashSet<DotnetDependency>());
            var firstProjectPath = fileSystem.Path.GetFullPath("/solution/first.csproj");
            var secondProjectPath = fileSystem.Path.GetFullPath("/solution/second.csproj");
            projectService.Setup(s => s.GetProjectDotnetDependencysAsync(firstProjectPath, null, false, null, null, null)).ReturnsAsync(reverse ? runtime : dev);
            projectService.Setup(s => s.GetProjectDotnetDependencysAsync(secondProjectPath, null, false, null, null, null)).ReturnsAsync(reverse ? dev : runtime);
            var package = Assert.Single(await new SolutionFileService(fileSystem, projectService.Object)
                .GetSolutionDotnetDependencys("/solution/test.slnx", null, false, null, null).ConfigureAwait(true));
            Assert.True(package.IsRuntimeReachable);
            Assert.True(package.IsDirectReference);
            Assert.False(package.IsDevDependency);
            Assert.Equal(Component.ComponentScope.Required, package.Scope);
            Assert.Contains(new KeyValuePair<string, string>("Child", "1.0.0"), package.GetDependencyEdges());
            Assert.Contains(new KeyValuePair<string, string>("Child", "2.0.0"), package.GetDependencyEdges());
        }

        [Fact]
        public async Task GetSolutionXProjectReferences_ReturnsProjectThatExists()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\SolutionPath\SolutionFile.slnx"), new MockFileData(@"<Solution>
  <Project Path=""Project\Project.csproj"" Type=""Classic C#"" />
</Solution>")},
                { XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), Helpers.GetEmptyProjectFile() },
            });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .Setup(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.slnx")).ConfigureAwait(true);

            Assert.Collection(projects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project\Project.csproj"), item));
        }

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
        public async Task GetSolutionProjectReferences_ReturnsListOfProjectsIncludingFSharpAndVBAndXSharp()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\SolutionPath\SolutionFile.sln"), new MockFileData(@"
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project1\Project1.csproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project2\Project2.fsproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project3\Project3.vbproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""CycloneDX"", ""Project4\Project4.xsproj"", ""{88DFA76C-1C0A-4A83-AA48-EA1D28A9ABED}""
                        ")},
                    { XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project2\Project2.fsproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project3\Project3.vbproj"), Helpers.GetEmptyProjectFile() },
                    { XFS.Path(@"c:\SolutionPath\Project4\Project4.xsproj"), Helpers.GetEmptyProjectFile() },
                });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .SetupSequence(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>())
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
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.vbproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project4\Project4.xsproj"), item));
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

        [Fact]
        public async Task GetSolutionFilterProjectReferences_ReturnsListOfProjects()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { XFS.Path(@"c:\SolutionPath\SolutionFile.slnf"), new MockFileData(@"
{
  ""solution"": {
    ""path"": ""SolutionFile.sln"",
    ""projects"": [
      ""Project1/Project1.csproj"",
      ""Project2/Project2.csproj"",
      ""Project3/Project3.csproj""
    ]
  }
}")},
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

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SolutionFile.slnf")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();

            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\Project3\Project3.csproj"), item));
        }

        [Fact]
        public async Task GetSolutionFilterProjectReferences_WhenLocationOfSlnfIsDifferentFromSln_ReturnsListOfProjects()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\SolutionPath\SrcPath\SolutionFileSrc.slnf"), new MockFileData(@"
{
  ""solution"": {
    ""path"": ""../SolutionFile.sln"",
    ""projects"": [
      ""SrcPath/Project1/Project1.csproj"",
      ""SrcPath/Project2/Project2.csproj"",
      ""SrcPath/Project3/Project3.csproj""
    ]
  }
}")},
                { XFS.Path(@"c:\SolutionPath\SrcPath\Project1\Project1.csproj"), Helpers.GetEmptyProjectFile() },
                { XFS.Path(@"c:\SolutionPath\SrcPath\Project2\Project2.csproj"), Helpers.GetEmptyProjectFile() },
                { XFS.Path(@"c:\SolutionPath\SrcPath\Project3\Project3.csproj"), Helpers.GetEmptyProjectFile() }
            });
            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService
                .SetupSequence(s => s.RecursivelyGetProjectReferencesAsync(It.IsAny<string>()))
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>())
                .ReturnsAsync(new HashSet<DotnetDependency>());
            var solutionFileService = new SolutionFileService(mockFileSystem, mockProjectFileService.Object);

            var projects = await solutionFileService.GetSolutionProjectReferencesAsync(XFS.Path(@"c:\SolutionPath\SrcPath\SolutionFileSrc.slnf")).ConfigureAwait(true);
            var sortedProjects = new List<string>(projects);
            sortedProjects.Sort();

            Assert.Collection(sortedProjects,
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\SrcPath\Project1\Project1.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\SrcPath\Project2\Project2.csproj"), item),
                item => Assert.Equal(XFS.Path(@"c:\SolutionPath\SrcPath\Project3\Project3.csproj"), item));
        }
    }
}
