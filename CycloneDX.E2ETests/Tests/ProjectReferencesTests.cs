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

using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;
using static VerifyXunit.Verifier;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Tests for <c>--include-project-references</c>.
    /// Verifies that packages from referenced projects are included in the BOM
    /// when the flag is set, and absent when it is not.
    /// </summary>
    [Collection("E2E")]
    public sealed class ProjectReferencesTests
    {
        private readonly E2EFixture _fixture;

        public ProjectReferencesTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProjectReference_NotIncludedByDefault()
        {
            // By default, packages from project references are promoted as direct dependencies
            // of the referencing project. The project reference itself (MyLib) is NOT added as
            // a BOM component — only its NuGet packages are.
            using var solution = await new SolutionBuilder("ProjRefDefaultSln")
                .AddProject("MyLib", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddProjectReference("../MyLib/MyLib.csproj"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            // Run against the single MyApp project (not the solution)
            var result = await _fixture.Runner.RunAsync(
                solution.ProjectFile("MyApp"),
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            // TestPkg.C comes from MyLib — it IS included because its packages are promoted
            Assert.Contains("TestPkg.C", result.BomContent);
            // But the MyLib project itself should NOT appear as a component
            Assert.DoesNotContain("MyLib", result.BomContent);
        }

        [Fact]
        public async Task ProjectReference_IncludedWithFlag()
        {
            // With --include-project-references the referenced project itself appears as a
            // BOM component in addition to its NuGet packages.
            using var solution = await new SolutionBuilder("ProjRefIncludeSln")
                .AddProject("MyLib", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddProjectReference("../MyLib/MyLib.csproj"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.ProjectFile("MyApp"),
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    IncludeProjectReferences = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
            // With the flag the MyLib project itself should appear as a component
            Assert.Contains("MyLib", result.BomContent);

            await Verify(result.BomContent);
        }
    }
}
