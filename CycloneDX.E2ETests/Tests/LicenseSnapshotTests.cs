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
    /// Full-BOM snapshot tests for license resolution.
    /// These complement the assertion-based tests in <see cref="LicenseResolutionTests"/>
    /// by capturing the complete BOM XML, so any structural or schema change to license
    /// output is caught as a snapshot diff.
    /// </summary>
    [Collection("E2E")]
    public sealed class LicenseSnapshotTests
    {
        private readonly E2EFixture _fixture;

        public LicenseSnapshotTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Phase 1: A package with a valid SPDX expression should emit &lt;id&gt;MIT&lt;/id&gt;.
        /// </summary>
        [Fact]
        public async Task SpdxLicense_ProducesValidBom()
        {
            using var solution = await new SolutionBuilder("LicenseSnapshotSpdxSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.SpdxLicense", "1.0.0"))
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
        /// Phase 3: A package with an embedded license file and <c>--include-license-text</c>
        /// should emit the base64-encoded license text in the BOM.
        /// </summary>
        [Fact]
        public async Task FileLicense_WithFlag_ProducesValidBom()
        {
            using var solution = await new SolutionBuilder("LicenseSnapshotFileFlagOnSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.FileLicense", "1.0.0"))
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
                    IncludeLicenseText = true,
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            await Verify(result.BomContent);
        }

        /// <summary>
        /// Phase 4: A package with only a deprecated &lt;licenseUrl&gt; should emit
        /// "Unknown - See URL" with the URL, and no embedded text.
        /// </summary>
        [Fact]
        public async Task UrlLicense_ProducesValidBom()
        {
            using var solution = await new SolutionBuilder("LicenseSnapshotUrlSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.UrlLicense", "1.0.0"))
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
    }
}
