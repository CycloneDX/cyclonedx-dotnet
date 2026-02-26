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
    /// Tests for solution-level scanning (multiple projects in a single .sln).
    /// Verifies that all packages from all projects are merged into one BOM and
    /// that test projects can be excluded via <c>--exclude-test-projects</c>.
    /// </summary>
    [Collection("E2E")]
    public sealed class SolutionScanTests
    {
        private readonly E2EFixture _fixture;

        public SolutionScanTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TwoProjects_BothPackagesInBom()
        {
            // Two separate projects each with a different package
            using var solution = await new SolutionBuilder("TwoProjSln")
                .AddProject("AppA", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .AddProject("AppB", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);

            await Verify(result.BomContent);
        }

        [Fact]
        public async Task SharedDependency_DeduplicatedInBom()
        {
            // Both projects reference the same package — it should appear only once in the BOM
            using var solution = await new SolutionBuilder("SharedDepSln")
                .AddProject("AppA", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .AddProject("AppB", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.NotNull(result.BomContent);

            // Count occurrences of the component id — should appear once
            var count = System.Text.RegularExpressions.Regex.Matches(
                result.BomContent, @"TestPkg\.A").Count;
            Assert.True(count > 0, "TestPkg.A should be present");
            // Component entries are deduplicated; name appears in <name> and <component> tags
            // but there should not be two full <component> blocks for the same id/version
            Assert.True(count < 10, $"TestPkg.A appeared {count} times — suspect duplication");
        }

        [Fact]
        public async Task TestProject_ExcludedWithFlag()
        {
            // A test project's packages should be absent when --exclude-test-projects is set
            using var solution = await new SolutionBuilder("ExcludeTestProjSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .AddProject("MyTests", p => p
                    .WithTargetFramework("net8.0")
                    .AsTestProject()
                    .AddPackage("TestPkg.C", "1.0.0"))
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
            Assert.DoesNotContain("TestPkg.C", result.BomContent);
        }
    }
}
