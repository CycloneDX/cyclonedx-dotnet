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
    /// Tests that the <c>--configuration</c> flag correctly filters conditional
    /// <c>PackageReference</c> items based on MSBuild configuration conditions.
    /// </summary>
    [Collection("E2E")]
    public sealed class ConditionalPackageTests
    {
        private readonly E2EFixture _fixture;

        // XML injected into the .csproj: TestPkg.A is always present;
        // TestPkg.C is only included when Configuration == Debug.
        private const string ConditionalPackageXml = """
            <ItemGroup>
              <PackageReference Include="TestPkg.A" Version="1.0.0" />
              <PackageReference Include="TestPkg.C" Version="1.0.0" Condition="'$(Configuration)' == 'Debug'" />
            </ItemGroup>
            """;

        public ConditionalPackageTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task NoConfiguration_BothPackagesAppearInBom()
        {
            // Without --configuration, dotnet restore uses the default configuration
            // (Debug), so both the unconditional and the Debug-only package are restored.
            using var solution = await new SolutionBuilder("ConditionalPkgNoConfigSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .WithRawXml(ConditionalPackageXml))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
        }

        [Fact]
        public async Task DebugConfiguration_BothPackagesAppearInBom()
        {
            // With --configuration Debug, the condition evaluates to true,
            // so TestPkg.C should be present alongside TestPkg.A.
            using var solution = await new SolutionBuilder("ConditionalPkgDebugSln")
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
                    Configuration = "Debug"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.Contains("TestPkg.C", result.BomContent);
        }

        [Fact]
        public async Task ReleaseConfiguration_OnlyUnconditionalPackageAppearInBom()
        {
            // With --configuration Release, the Debug-only condition is false,
            // so TestPkg.C must NOT appear; TestPkg.A must still be present.
            using var solution = await new SolutionBuilder("ConditionalPkgReleaseSln")
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
                    Configuration = "Release"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("TestPkg.A", result.BomContent);
            Assert.DoesNotContain("TestPkg.C", result.BomContent);
        }
    }
}
