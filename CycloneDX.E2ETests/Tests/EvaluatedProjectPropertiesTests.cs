// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Project properties are evaluated by MSBuild (#1141), so values that a repository sets once
    /// for all projects in a Directory.Build.props are used. Reading only the project files missed
    /// them: the root version became 0.0.0, referenced projects appeared twice (once under the
    /// name NuGet uses, once under the project file name) and test projects were not excluded.
    /// </summary>
    [Collection("E2E")]
    public sealed class EvaluatedProjectPropertiesTests
    {
        private readonly E2EFixture _fixture;

        public EvaluatedProjectPropertiesTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VersionAndAssemblyNameFromDirectoryBuildProps_AreUsedForProjectAndReferences()
        {
            // One version and one naming scheme for the whole repository. NuGet names the
            // referenced project by its package ID, which defaults to the assembly name.
            using var solution = await new SolutionBuilder("CentralVersionSln")
                .AddFile("Directory.Build.props", """
                    <Project>
                      <PropertyGroup>
                        <Version>2.4.1</Version>
                        <AssemblyName>Contoso.$(MSBuildProjectName)</AssemblyName>
                      </PropertyGroup>
                    </Project>
                    """)
                .AddProject("Core", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .AddProject("App", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddProjectReference("../Core/Core.csproj"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.ProjectFile("App"),
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    Recursive = true,
                    IncludeProjectReferences = true,
                    OutputFormat = "json"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            using var bom = JsonDocument.Parse(result.BomContent);

            var root = bom.RootElement.GetProperty("metadata").GetProperty("component");
            Assert.Equal("App", root.GetProperty("name").GetString());
            Assert.Equal("2.4.1", root.GetProperty("version").GetString());

            // The referenced project appears once, under the name NuGet uses for it
            var coreComponents = bom.RootElement.GetProperty("components").EnumerateArray()
                .Select(component => $"{component.GetProperty("name").GetString()}@{component.GetProperty("version").GetString()}")
                .Where(component => component.Contains("Core"))
                .ToList();
            Assert.Equal(new[] { "Contoso.Core@2.4.1" }, coreComponents);

            var appDependencies = bom.RootElement.GetProperty("dependencies").EnumerateArray()
                .Single(dependency => dependency.GetProperty("ref").GetString() == root.GetProperty("bom-ref").GetString())
                .GetProperty("dependsOn").EnumerateArray()
                .Select(dependency => dependency.GetString());
            Assert.Contains("Contoso.Core@2.4.1", appDependencies);
        }

        [Fact]
        public async Task TestProjectsGettingTheTestSdkFromDirectoryBuildProps_AreExcluded()
        {
            // Every *.Tests project gets its test packages from the Directory.Build.props, so its
            // project file contains nothing test specific (#669)
            await _fixture.PushPackageAsync(NupkgBuilder.Build("Microsoft.NET.Test.Sdk", "17.11.1"));

            using var solution = await new SolutionBuilder("TestSdkFromPropsSln")
                .AddFile("Directory.Build.props", """
                    <Project>
                      <ItemGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
                        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
                        <PackageReference Include="TestPkg.C" Version="1.0.0" />
                      </ItemGroup>
                    </Project>
                    """)
                .AddProject("App", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .AddProject("App.Tests", p => p
                    .WithTargetFramework("net8.0")
                    .AddProjectReference("../App/App.csproj"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeTestProjects = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            // Neither the test SDK nor the other test package (e.g. the test framework) of App.Tests
            Assert.DoesNotContain("Microsoft.NET.Test.Sdk", result.BomContent);
            Assert.DoesNotContain("TestPkg.C", result.BomContent);
        }
    }
}
