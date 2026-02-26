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
    /// Tests for various CLI flags that affect the BOM metadata:
    /// <c>--spec-version</c>, <c>--set-name</c>, <c>--set-version</c>,
    /// <c>--set-type</c>, and <c>--no-serial-number</c>.
    /// </summary>
    [Collection("E2E")]
    public sealed class CliOptionTests
    {
        private readonly E2EFixture _fixture;

        public CliOptionTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task NoSerialNumber_OmitsSerialNumberFromBom()
        {
            using var solution = await new SolutionBuilder("NoSerialSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    NoSerialNumber = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("serialNumber", result.BomContent);
        }

        [Fact]
        public async Task SetName_AppearsInBomMetadata()
        {
            using var solution = await new SolutionBuilder("SetNameSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    SetName = "MyCustomAppName"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("MyCustomAppName", result.BomContent);
        }

        [Fact]
        public async Task SetVersion_AppearsInBomMetadata()
        {
            using var solution = await new SolutionBuilder("SetVersionSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    SetVersion = "3.7.0"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("3.7.0", result.BomContent);
        }

        [Fact]
        public async Task SetType_AppearsInBomMetadata()
        {
            using var solution = await new SolutionBuilder("SetTypeSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    SetType = "library"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("library", result.BomContent);
        }

        [Fact]
        public async Task SpecVersion14_ProducesV14Bom()
        {
            using var solution = await new SolutionBuilder("SpecV14Sln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    SpecVersion = "1.4"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("cyclonedx.org/schema/bom/1.4", result.BomContent);
        }

        [Fact]
        public async Task SpecVersion16_ProducesV16Bom()
        {
            using var solution = await new SolutionBuilder("SpecV16Sln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    SpecVersion = "1.6"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("cyclonedx.org/schema/bom/1.6", result.BomContent);
        }
    }
}
