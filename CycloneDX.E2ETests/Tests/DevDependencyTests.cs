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
    /// Tests for dev/build-only dependencies (PrivateAssets="all").
    /// Dev dependencies are always included in the BOM with scope="excluded".
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
        public async Task DevDependency_IncludedWithScopeExcluded()
        {
            // Dev dependencies must appear in <components> with <scope>excluded</scope>
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
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.Dev", result.BomContent);
            // Dev dep must appear with scope=excluded
            Assert.Contains("<scope>excluded</scope>", result.BomContent);
            // No formulation — dev deps stay in components
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Fact]
        public async Task DevDependency_ScopeExcluded_RuntimePackage_ScopeRequired()
        {
            // Runtime packages must be scope=required; dev deps scope=excluded
            using var solution = await new SolutionBuilder("DevDepScopeSln")
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

            // TestPkg.A (runtime) must appear before scope=required; TestPkg.Dev before scope=excluded.
            // The simplest check: both scope values are present in the BOM.
            Assert.Contains("<scope>required</scope>", result.BomContent);
            Assert.Contains("<scope>excluded</scope>", result.BomContent);
        }

        [Fact]
        public async Task ExcludeDevFlag_IsDeprecatedAndHasNoEffect()
        {
            // --exclude-dev is deprecated; passing it must not change the output
            using var solution = await new SolutionBuilder("DevDepFlagSln")
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
            // Dev dep must still be present with scope=excluded, not omitted
            Assert.Contains("TestPkg.Dev", result.BomContent);
            Assert.Contains("<scope>excluded</scope>", result.BomContent);
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Fact]
        public async Task TransitiveOfDevDependency_IncludedInComponents()
        {
            // A transitive dependency pulled in only through a dev dep must also
            // appear in <components> (the BFS exclusion logic has been removed).
            using var solution = await new SolutionBuilder("TransitiveDevDepSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.DevWithDep", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.DevWithDep", result.BomContent);
            Assert.Contains("TestPkg.DevTransitive", result.BomContent);
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }
    }
}
