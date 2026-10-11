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
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class RecursiveLegacyReachabilityTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SyntheticProjectRoot_DoesNotPromoteKnownDevelopmentChild(bool excludeDev)
        {
            const string projectPath = "/solution/root.csproj";
            var project = new DotnetDependency
            {
                Name = "ReferencedProject", Version = "1.0.0", DependencyType = DependencyType.Project,
                IsDirectReference = true, Scope = Component.ComponentScope.Required,
                Dependencies = new Dictionary<string, string> { ["Runtime"] = "1.0.0", ["Dev"] = "1.0.0" }
            };
            Assert.Null(project.IsRuntimeReachable);
            var packages = new HashSet<DotnetDependency>
            {
                project,
                new() { Name = "Runtime", Version = "1.0.0", IsRuntimeReachable = true, IsNonDevelopmentReachable = true, Scope = Component.ComponentScope.Required },
                new() { Name = "Dev", Version = "1.0.0", IsDevDependency = true, IsRuntimeReachable = false, IsNonDevelopmentReachable = false, Scope = Component.ComponentScope.Excluded }
            };
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { [projectPath] = "<Project />" });
            var projectService = new Mock<IProjectFileService>();
            projectService.Setup(s => s.RecursivelyGetProjectDotnetDependencysAsync(projectPath,
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(packages);
            projectService.Setup(s => s.GetComponent(It.IsAny<DotnetDependency>())).Returns((DotnetDependency dependency) => new Component
            {
                Name = dependency.Name, Version = dependency.Version, Type = Component.Classification.Application,
                Scope = dependency.Scope, BomRef = $"pkg:nuget/{dependency.Name}@{dependency.Version}"
            });
            var nugetService = new Mock<INugetService>();
            nugetService.Setup(s => s.GetComponentAsync(It.IsAny<DotnetDependency>())).Returns((DotnetDependency dependency) => Task.FromResult(new Component
            {
                Name = dependency.Name, Version = dependency.Version, Type = Component.Classification.Library,
                Scope = dependency.Scope, BomRef = $"pkg:nuget/{dependency.Name}@{dependency.Version}"
            }));
            var factory = new Mock<INugetServiceFactory>();
            factory.Setup(f => f.Create(It.IsAny<RunOptions>(), It.IsAny<IFileSystem>(), It.IsAny<IGithubService>(), It.IsAny<List<string>>())).Returns(nugetService.Object);
            var runner = new Runner(fileSystem, null, null, null, null, projectService.Object, null, factory.Object);

            var exitCode = await runner.HandleCommandAsync(new RunOptions
            {
                SolutionOrProjectFile = projectPath, scanProjectReferences = true, disablePackageRestore = true,
                excludeDev = excludeDev, outputDirectory = "/bom/"
            }).ConfigureAwait(true);

            Assert.Equal((int)ExitCode.OK, exitCode);
            var bom = runner.LastGeneratedBom;
            Assert.Equal(Component.ComponentScope.Required, Assert.Single(bom.Components, c => c.Name == "Runtime").Scope);
            Assert.DoesNotContain(bom.Components, c => c.Name == "ReferencedProject");
            if (excludeDev)
            {
                Assert.DoesNotContain(bom.Components, c => c.Name == "Dev");
                Assert.DoesNotContain(bom.Dependencies, d => d.Ref == "pkg:nuget/Dev@1.0.0" ||
                    (d.Dependencies ?? []).Any(child => child.Ref == "pkg:nuget/Dev@1.0.0"));
            }
            else
                Assert.Equal(Component.ComponentScope.Excluded, Assert.Single(bom.Components, c => c.Name == "Dev").Scope);
        }
    }
}
