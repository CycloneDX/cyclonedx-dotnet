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

using System;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// End-to-end tests for license resolution behavior.
    /// Covers all four phases: SPDX expression, GitHub lookup (mocked via vocabulary packages
    /// that have no GitHub URLs), license file embedding, and licenseUrl fallback.
    ///
    /// These tests will fail until --include-license-text is implemented.
    /// </summary>
    [Collection("E2E")]
    public sealed class LicenseResolutionTests
    {
        private readonly E2EFixture _fixture;

        public LicenseResolutionTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        // -----------------------------------------------------------------------------------------
        // Phase 1 — SPDX expression
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task SpdxExpression_EmitsLicenseId()
        {
            using var solution = await new SolutionBuilder("SpdxLicenseSln")
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
            Assert.Contains("<id>MIT</id>", result.BomContent);
            Assert.DoesNotContain("<text>", result.BomContent);
        }

        // -----------------------------------------------------------------------------------------
        // Phase 3 — license file with --include-license-text
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task LicenseFile_WithFlag_EmbedsBase64Text()
        {
            // The expected base64 content of the LICENSE.txt embedded in TestPkg.FileLicense
            var expectedContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "MIT License\n\nCopyright (c) CycloneDX E2E Tests\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software."));

            using var solution = await new SolutionBuilder("FileLicenseFlagOnSln")
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
            Assert.Contains(expectedContent, result.BomContent);
            Assert.Contains("text/plain", result.BomContent);
            Assert.Contains("base64", result.BomContent);
        }

        [Fact]
        public async Task LicenseFileMd_WithFlag_UsesMarkdownContentType()
        {
            using var solution = await new SolutionBuilder("FileLicenseMdSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.FileLicenseMd", "1.0.0"))
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
            Assert.Contains("text/markdown", result.BomContent);
        }

        [Fact]
        public async Task LicenseFile_WithoutFlag_EmitsNoLicense()
        {
            // Phase 3 inactive — license file should be ignored, no URL to fall back to,
            // so no license node should appear for this package.
            using var solution = await new SolutionBuilder("FileLicenseFlagOffSln")
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
                    IncludeLicenseText = false,
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");

            // Extract the <component> block for TestPkg.FileLicense so the assertion is scoped
            // to that package only and is not affected by other packages that may appear in the BOM.
            var bom = result.BomContent;
            var nameMarker = "<name>TestPkg.FileLicense</name>";
            var nameIdx = bom.IndexOf(nameMarker, StringComparison.Ordinal);
            Assert.True(nameIdx >= 0, "TestPkg.FileLicense component not found in BOM");

            // Walk back to the opening <component tag
            var componentStart = bom.LastIndexOf("<component", nameIdx, StringComparison.Ordinal);
            // Walk forward to the closing </component>
            var componentEnd = bom.IndexOf("</component>", nameIdx, StringComparison.Ordinal);
            Assert.True(componentStart >= 0 && componentEnd >= 0, "Could not locate <component> boundaries");

            var componentBlock = bom.Substring(componentStart, componentEnd - componentStart + "</component>".Length);
            Assert.DoesNotContain("<licenses>", componentBlock);
        }

        [Fact]
        public async Task FileLicenseWithAkaMsDeprecatedUrl_WithoutFlag_EmitsNoLicense()
        {
            // When NuGet packs a <license type="file"> package it auto-inserts
            // <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>.
            // That URL is a dead redirect, not a real license URL.
            // Without --include-license-text, Phase 3 is inactive, and Phase 4 must NOT
            // fall back to the aka.ms stub — no <licenses> node should appear.
            //
            // This test FAILS until the aka.ms guard is implemented.
            using var solution = await new SolutionBuilder("FileLicenseDeprecatedUrlFlagOffSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.FileLicenseDeprecatedUrl", "1.0.0"))
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
                    IncludeLicenseText = false,
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");

            var bom = result.BomContent;
            var nameMarker = "<name>TestPkg.FileLicenseDeprecatedUrl</name>";
            var nameIdx = bom.IndexOf(nameMarker, StringComparison.Ordinal);
            Assert.True(nameIdx >= 0, "TestPkg.FileLicenseDeprecatedUrl component not found in BOM");

            var componentStart = bom.LastIndexOf("<component", nameIdx, StringComparison.Ordinal);
            var componentEnd = bom.IndexOf("</component>", nameIdx, StringComparison.Ordinal);
            Assert.True(componentStart >= 0 && componentEnd >= 0, "Could not locate <component> boundaries");

            var componentBlock = bom.Substring(componentStart, componentEnd - componentStart + "</component>".Length);
            Assert.DoesNotContain("<licenses>", componentBlock);
            Assert.DoesNotContain("aka.ms", componentBlock);
        }

        [Fact]
        public async Task SpdxExpression_WithIncludeLicenseTextFlag_StillEmitsLicenseId()
        {
            // SPDX expression (Phase 1) must win even when --include-license-text is set.
            // The TestPkg.SpdxLicense package has no embedded file, but even if it did,
            // Phase 1 takes priority over Phase 3.
            using var solution = await new SolutionBuilder("SpdxLicenseFlagOnSln")
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
                    IncludeLicenseText = true,
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Contains("<id>MIT</id>", result.BomContent);
            // Phase 1 won — no embedded text should appear
            Assert.DoesNotContain("<text>", result.BomContent);
        }

        // -----------------------------------------------------------------------------------------
        // Phase 4 — licenseUrl fallback
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task LicenseUrl_EmitsUnknownSeeUrlEntry()
        {
            // Phase 4: deprecated <licenseUrl> present — should emit the URL entry even without
            // --include-license-text.
            using var solution = await new SolutionBuilder("UrlLicenseSln")
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
            Assert.Contains("Unknown - See URL", result.BomContent);
            Assert.Contains("https://opensource.org/licenses/MIT", result.BomContent);
        }

        [Fact]
        public async Task NoLicenseInfo_EmitsNoLicenseNode()
        {
            // Phase 4 fix: no license metadata at all — should produce no <licenses> node,
            // not a null-URL stub entry.
            using var solution = await new SolutionBuilder("NoLicenseSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.NoLicense", "1.0.0"))
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
            Assert.DoesNotContain("<licenses>", result.BomContent);
        }

        // -----------------------------------------------------------------------------------------
        // Mixed — all four phases in one solution
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task MixedLicenses_AllResolvedCorrectly()
        {
            // A single solution referencing all four vocabulary packages simultaneously.
            // With --include-license-text:
            //   TestPkg.SpdxLicense  → Phase 1 wins → <id>MIT</id>
            //   TestPkg.FileLicense  → Phase 3 wins → base64 embedded text
            //   TestPkg.UrlLicense   → Phase 4 wins → "Unknown - See URL" with URL
            //   TestPkg.NoLicense    → no license node
            //
            // Exactly 3 <licenses> blocks expected in the BOM.
            using var solution = await new SolutionBuilder("MixedLicensesSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.SpdxLicense", "1.0.0")
                    .AddPackage("TestPkg.FileLicense", "1.0.0")
                    .AddPackage("TestPkg.UrlLicense", "1.0.0")
                    .AddPackage("TestPkg.NoLicense", "1.0.0"))
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

            var bom = result.BomContent;

            // Phase 1: SPDX expression resolved
            Assert.Contains("<id>MIT</id>", bom);

            // Phase 3: license file embedded as base64
            Assert.Contains("base64", bom);
            Assert.Contains("text/plain", bom);

            // Phase 4: licenseUrl fallback
            Assert.Contains("Unknown - See URL", bom);
            Assert.Contains("https://opensource.org/licenses/MIT", bom);

            // Exactly 3 packages should have a <licenses> block; NoLicense should not add one.
            var licenseBlockCount = bom.Split(new[] { "<licenses>" }, StringSplitOptions.None).Length - 1;
            Assert.Equal(3, licenseBlockCount);
        }
    }
}
