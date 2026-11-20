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
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class Issue1143_DuplicateDependsOn
    {
        /// <summary>
        /// One project resolves Shared's edge to Tool@2.0.0, another lacks Tool in its graph
        /// (PrivateAssets="all") and keeps the declared 1.0.0. After merging, both edges resolve to
        /// the only Tool component, which must be listed once in dependsOn.
        /// </summary>
        [Fact]
        public async Task MergedEdgesResolvingToSameComponent_AreListedOnce()
        {
            var projectPath = MockUnixSupport.Path("c:/solution/project.csproj");
            var shared = new DotnetDependency { Name = "Shared", Version = "1.0.0", IsDirectReference = true };
            shared.AddDependencyEdge("Tool", "2.0.0");
            shared.AddDependencyEdge("Tool", "1.0.0");
            var packages = new HashSet<DotnetDependency> { shared, new() { Name = "Tool", Version = "2.0.0" } };

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { [projectPath] = "<Project />" });
            projectPath = fileSystem.Path.GetFullPath(projectPath);
            var projectService = new Mock<IProjectFileService>();
            projectService.Setup(s => s.GetProjectDotnetDependencysAsync(projectPath, It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(packages);
            var nugetService = new Mock<INugetService>();
            nugetService.Setup(s => s.GetComponentAsync(It.IsAny<DotnetDependency>())).Returns((DotnetDependency dependency) => Task.FromResult(new Component
            {
                Name = dependency.Name, Version = dependency.Version, Type = Component.Classification.Library,
                BomRef = $"pkg:nuget/{dependency.Name}@{dependency.Version}"
            }));
            var factory = new Mock<INugetServiceFactory>();
            factory.Setup(f => f.Create(It.IsAny<RunOptions>(), It.IsAny<IFileSystem>(), It.IsAny<IGithubService>(), It.IsAny<List<string>>())).Returns(nugetService.Object);
            var runner = new Runner(fileSystem, null, null, null, null, projectService.Object, null, factory.Object);

            var exitCode = await runner.HandleCommandAsync(new RunOptions
            {
                SolutionOrProjectFile = projectPath, disablePackageRestore = true, outputDirectory = "/bom/"
            }).ConfigureAwait(true);

            Assert.Equal((int)ExitCode.OK, exitCode);
            var sharedNode = Assert.Single(runner.LastGeneratedBom.Dependencies, d => d.Ref == "pkg:nuget/Shared@1.0.0");
            Assert.Equal("pkg:nuget/Tool@2.0.0", Assert.Single(sharedNode.Dependencies).Ref);
        }
    }
}
