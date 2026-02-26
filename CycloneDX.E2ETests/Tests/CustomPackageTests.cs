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
    /// Demonstrates per-test custom packages pushed to BaGetter on demand.
    /// This is the escape hatch for scenarios that cannot be expressed with
    /// the shared vocabulary packages.
    /// </summary>
    [Collection("E2E")]
    public sealed class CustomPackageTests
    {
        private readonly E2EFixture _fixture;

        public CustomPackageTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CustomPackage_AppearsInBom()
        {
            // Build and push a bespoke package at test time
            var customNupkg = NupkgBuilder.Build("MyOrg.CustomLib", "4.2.0",
                description: "A custom package for this specific test");

            await _fixture.PushPackageAsync(customNupkg);

            using var solution = await new SolutionBuilder("CustomPkgSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("MyOrg.CustomLib", "4.2.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("MyOrg.CustomLib", result.BomContent);
            Assert.Contains("4.2.0", result.BomContent);
        }

        [Fact]
        public async Task CustomPackageWithTransitiveDep_BothAppearInBom()
        {
            // Build a package with a dependency on an existing vocab package
            var customNupkg = NupkgBuilder.Build("MyOrg.Wrapper", "1.0.0",
                description: "Wraps TestPkg.A",
                dependencies: new[] { new NupkgDependency("TestPkg.A", "1.0.0") });

            await _fixture.PushPackageAsync(customNupkg);

            using var solution = await new SolutionBuilder("CustomTransitiveSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("MyOrg.Wrapper", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("MyOrg.Wrapper", result.BomContent);
            Assert.Contains("TestPkg.A", result.BomContent);
        }
    }
}
