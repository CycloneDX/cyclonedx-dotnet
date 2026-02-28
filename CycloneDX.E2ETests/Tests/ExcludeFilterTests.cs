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

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Tests for <c>--exclude-filter</c>.
    /// Verifies that packages can be excluded by exact name@version or by name only
    /// (all versions), and that transitive dependencies are also removed when their
    /// parent is excluded.
    /// </summary>
    [Collection("E2E")]
    public sealed class ExcludeFilterTests
    {
        private readonly E2EFixture _fixture;

        public ExcludeFilterTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Excluding by <c>name@version</c> removes only that specific version.
        /// The other package still appears in the BOM.
        /// </summary>
        [Fact]
        public async Task ExcludeByNameAndVersion_RemovesSpecificPackage()
        {
            using var solution = await new SolutionBuilder("ExcludeByVersionSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeFilter = "TestPkg.A@1.0.0"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
        }

        /// <summary>
        /// Excluding by <c>name</c> only (without a version) removes all versions of that package.
        /// </summary>
        [Fact]
        public async Task ExcludeByNameOnly_RemovesAllVersionsOfPackage()
        {
            using var solution = await new SolutionBuilder("ExcludeByNameSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeFilter = "TestPkg.A"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
        }

        /// <summary>
        /// Excluding a package that has transitive dependencies also removes those orphaned
        /// transitive dependencies from the BOM.
        /// </summary>
        [Fact]
        public async Task ExcludeWithTransitiveDeps_RemovesOrphanedTransitiveDeps()
        {
            // TestPkg.B depends on TestPkg.A — excluding TestPkg.B should also remove TestPkg.A
            using var solution = await new SolutionBuilder("ExcludeTransitiveSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.B", "1.0.0")
                    .AddPackage("TestPkg.C", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeFilter = "TestPkg.B@1.0.0"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.B", result.BomContent);
            // TestPkg.A was only reachable via TestPkg.B, so it should also be removed
            Assert.DoesNotContain("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
        }

        /// <summary>
        /// Mixed filter combining name-only and name@version entries in a single comma-separated list.
        /// </summary>
        [Fact]
        public async Task ExcludeMixedFilter_RemovesAllMatchingPackages()
        {
            using var solution = await new SolutionBuilder("ExcludeMixedSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.C", "1.0.0")
                    .AddPackage("TestPkg.Dev", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeFilter = "TestPkg.A,TestPkg.C@1.0.0"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.A", result.BomContent);
            Assert.DoesNotContain("TestPkg.C", result.BomContent);
            Assert.Contains("TestPkg.Dev", result.BomContent);
        }
    }
}
