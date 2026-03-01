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
    /// Regression tests for issue #1025:
    /// Transitive dependencies are silently dropped from the SBOM when a package's .nuspec
    /// lists its dependency with different casing than the canonical package ID in the lock file.
    ///
    /// NuGet package IDs are case-insensitive, but project.assets.json preserves the casing
    /// from each package's own .nuspec when writing the "dependencies" dict inside "targets".
    /// For example, if "TestPkg.CaseMismatch" was published with a .nuspec that declares its
    /// dependency as "testpkg.a" instead of "TestPkg.A", NuGet writes "testpkg.a" verbatim
    /// into the assets file.
    ///
    /// The bug in Runner.cs uses ordinal string comparison when checking whether a dependency
    /// key has a corresponding package, causing "testpkg.a" to be treated as having no match
    /// and removed from the dependency graph unconditionally.
    /// </summary>
    [Collection("E2E")]
    public sealed class Issue1025Tests
    {
        private readonly E2EFixture _fixture;

        public Issue1025Tests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TransitiveDep_ShouldAppearInBom_WhenNuspecDepIdHasWrongCasing()
        {
            // Build a package whose .nuspec intentionally uses wrong casing for its dependency.
            // NuGet copies this casing verbatim into project.assets.json's targets->dependencies dict,
            // which is what triggers the bug: "testpkg.a" != "TestPkg.A" under ordinal comparison.
            var caseMismatchPkg = NupkgBuilder.Build(
                "TestPkg.CaseMismatch", "1.0.0",
                description: "Package with wrong-cased dep in .nuspec (reproduces issue #1025)",
                dependencies: new[] { new NupkgDependency("testpkg.a", "1.0.0") });

            await _fixture.PushPackageAsync(caseMismatchPkg);

            using var solution = await new SolutionBuilder("Issue1025Sln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.CaseMismatch", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");

            // TestPkg.A must appear as a component — it is the transitive dependency.
            Assert.Contains("TestPkg.A", result.BomContent);

            // The dependency graph must show TestPkg.CaseMismatch -> TestPkg.A.
            // If the bug is present, the inner <dependency> element will be missing because
            // "testpkg.a" was stripped from the dependency dict by the ordinal Except() check.
            Assert.Contains(
                "<dependency ref=\"pkg:nuget/TestPkg.CaseMismatch@1.0.0\">",
                result.BomContent);
            Assert.Contains(
                """
                <dependency ref="pkg:nuget/TestPkg.CaseMismatch@1.0.0">
                      <dependency ref="pkg:nuget/TestPkg.A@1.0.0" />
                """,
                result.BomContent);
        }
    }
}
