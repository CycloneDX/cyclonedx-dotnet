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
    /// Tests for dev/build-only dependencies (PrivateAssets="all").
    /// Verifies that <c>--exclude-dev</c> omits packages that are exclusively
    /// consumed at build time and not shipped with the application.
    /// </summary>
    [Collection("E2E")]
    public sealed class DevDependencyTests
    {
        private readonly E2EFixture _fixture;

        public DevDependencyTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DevDependency_IncludedByDefault()
        {
            // Without --exclude-dev, dev dependencies SHOULD appear in the BOM
            using var solution = await new SolutionBuilder("DevDepDefaultSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.Dev", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.Dev", result.BomContent);
            Assert.Contains("TestPkg.A", result.BomContent);
        }

        [Fact]
        public async Task DevDependency_ExcludedWithFlag()
        {
            // With --exclude-dev, packages marked PrivateAssets="all" should be absent
            using var solution = await new SolutionBuilder("DevDepExcludeSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.Dev", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeDev = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.DoesNotContain("TestPkg.Dev", result.BomContent);

            await Verify(result.BomContent);
        }

        [Fact]
        public async Task OnlyDevDependencies_ExcludeDevProducesEmptyComponents()
        {
            // A project with only dev deps + --exclude-dev → no components section (or empty)
            using var solution = await new SolutionBuilder("OnlyDevDepSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Dev", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeDev = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.Dev", result.BomContent);
        }
    }
}
