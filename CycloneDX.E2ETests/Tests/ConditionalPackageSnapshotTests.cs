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
    /// Full-BOM snapshot tests for the <c>--configuration</c> flag.
    /// Complements the assertion-based tests in <see cref="ConditionalPackageTests"/> by
    /// capturing the complete BOM XML, so any structural change to the output is caught
    /// as a snapshot diff.
    ///
    /// The project has two packages:
    ///   TestPkg.A — always included
    ///   TestPkg.C — only included when Configuration == Debug
    /// </summary>
    [Collection("E2E")]
    public sealed class ConditionalPackageSnapshotTests
    {
        private readonly E2EFixture _fixture;

        private const string ConditionalPackageXml = """
            <ItemGroup>
              <PackageReference Include="TestPkg.A" Version="1.0.0" />
              <PackageReference Include="TestPkg.C" Version="1.0.0" Condition="'$(Configuration)' == 'Debug'" />
            </ItemGroup>
            """;

        public ConditionalPackageSnapshotTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Without <c>--configuration</c>, dotnet restore defaults to Debug, so both
        /// the unconditional package and the Debug-only package appear in the BOM.
        /// </summary>
        [Fact]
        public async Task NoConfigurationFlag_ProducesValidBom()
        {
            using var solution = await new SolutionBuilder("ConditionalSnapshotNoFlagSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .WithRawXml(ConditionalPackageXml))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    NoSerialNumber = true,
                    DisableHashComputation = true,
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            await Verify(result.BomContent);
        }

        /// <summary>
        /// With <c>--configuration Release</c>, the Debug-only condition is false, so
        /// only the unconditional package appears in the BOM.
        /// </summary>
        [Fact]
        public async Task ReleaseConfigurationFlag_ProducesValidBom()
        {
            using var solution = await new SolutionBuilder("ConditionalSnapshotReleaseSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .WithRawXml(ConditionalPackageXml))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    NoSerialNumber = true,
                    DisableHashComputation = true,
                    Configuration = "Release",
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            await Verify(result.BomContent);
        }
    }
}
