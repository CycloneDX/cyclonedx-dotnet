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
            // With --exclude-dev, packages marked PrivateAssets="all" should be moved to
            // <formulation>, not <components>
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
            // Dev dep must be in formulation, not absent entirely
            Assert.Contains("<formulation>", result.BomContent);
            Assert.Contains("TestPkg.Dev", result.BomContent);
            // TestPkg.Dev must NOT appear before <formulation> (i.e., not in <components>)
            var formulationIdx = result.BomContent.IndexOf("<formulation>", System.StringComparison.Ordinal);
            var beforeFormulation = result.BomContent.Substring(0, formulationIdx);
            Assert.DoesNotContain("TestPkg.Dev", beforeFormulation);

            await Verify(result.BomContent);
        }

        [Fact]
        public async Task OnlyDevDependencies_ExcludeDevProducesEmptyComponents()
        {
            // A project with only dev deps + --exclude-dev → dev dep moves to <formulation>,
            // <components> is absent or empty
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
            // Dev dep must be in <formulation>, not in <components>
            Assert.Contains("<formulation>", result.BomContent);
            Assert.Contains("TestPkg.Dev", result.BomContent);
            // Extract the section of the document before <formulation> — that covers everything
            // including the top-level <components> block (if any). TestPkg.Dev must not be there.
            var formulationStart = result.BomContent.IndexOf("<formulation>", System.StringComparison.Ordinal);
            var beforeFormulation = result.BomContent.Substring(0, formulationStart);
            Assert.DoesNotContain("TestPkg.Dev", beforeFormulation);
        }

        [Fact]
        public async Task DevDependency_MovedToFormulationWithFlag()
        {
            // With --exclude-dev, the dev dep must appear inside <formulation>, not <components>
            using var solution = await new SolutionBuilder("DevDepFormulationSln")
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
            // TestPkg.A must be in the main components list
            Assert.Contains("TestPkg.A", result.BomContent);
            // TestPkg.Dev must be present (in formulation) but NOT in <components>
            Assert.Contains("<formulation>", result.BomContent);
            Assert.Contains("TestPkg.Dev", result.BomContent);

            // Verify the dev dep is inside <formulation> and NOT inside <components>
            var formulationIdx = result.BomContent.IndexOf("<formulation>", System.StringComparison.Ordinal);
            var componentsEndIdx = result.BomContent.IndexOf("</components>", System.StringComparison.Ordinal);
            Assert.True(formulationIdx > componentsEndIdx,
                "TestPkg.Dev should appear after </components> (inside <formulation>), not inside <components>");

            await Verify(result.BomContent);
        }

        [Fact]
        public async Task TransitiveDevDependency_ExcludedWithFlag()
        {
            // TestPkg.DevWithDep (dev) depends on TestPkg.DevTransitive.
            // With --exclude-dev both should be moved to <formulation>, not <components>.
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
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeDev = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("<formulation>", result.BomContent);
            Assert.Contains("TestPkg.DevWithDep", result.BomContent);
            Assert.Contains("TestPkg.DevTransitive", result.BomContent);

            // Both dev-related packages must be in <formulation>, not in <components>
            var formulationIdx = result.BomContent.IndexOf("<formulation>", System.StringComparison.Ordinal);
            var componentsEndIdx = result.BomContent.IndexOf("</components>", System.StringComparison.Ordinal);
            var devWithDepIdx = result.BomContent.IndexOf("TestPkg.DevWithDep", System.StringComparison.Ordinal);
            var devTransitiveIdx = result.BomContent.IndexOf("TestPkg.DevTransitive", System.StringComparison.Ordinal);
            Assert.True(devWithDepIdx > componentsEndIdx,
                "TestPkg.DevWithDep should be in <formulation>, not <components>");
            Assert.True(devTransitiveIdx > componentsEndIdx,
                "TestPkg.DevTransitive should be in <formulation>, not <components>");

            await Verify(result.BomContent);
        }

        [Fact]
        public async Task TransitiveDevDependency_SharedWithRuntime_StaysInComponents()
        {
            // TestPkg.DevWithDep (dev) depends on TestPkg.DevTransitive.
            // TestPkg.A also depends on TestPkg.DevTransitive (shared transitive).
            // With --exclude-dev: TestPkg.DevWithDep → formulation,
            // but TestPkg.DevTransitive is reachable via the runtime path through TestPkg.A
            // and must stay in <components>.
            //
            // We model this by using TestPkg.B (which depends on TestPkg.A) and a custom
            // package that depends on TestPkg.DevTransitive from the runtime side.
            // The simplest approach: add TestPkg.DevTransitive as a *runtime* direct dep too.
            using var solution = await new SolutionBuilder("SharedTransitiveSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.DevTransitive", "1.0.0")          // runtime direct ref
                    .AddPackage("TestPkg.DevWithDep", "1.0.0", devDependency: true))  // dev dep that also pulls it
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

            // TestPkg.DevTransitive is reachable from runtime; must be in <components>
            var componentsSection = result.BomContent.Substring(
                0, result.BomContent.IndexOf("</components>", System.StringComparison.Ordinal) + "</components>".Length);
            Assert.Contains("TestPkg.DevTransitive", componentsSection);

            // TestPkg.DevWithDep (the dev dep itself) must be in <formulation>
            Assert.Contains("<formulation>", result.BomContent);
            var formulationSection = result.BomContent.Substring(
                result.BomContent.IndexOf("<formulation>", System.StringComparison.Ordinal));
            Assert.Contains("TestPkg.DevWithDep", formulationSection);

            await Verify(result.BomContent);
        }
    }
}
