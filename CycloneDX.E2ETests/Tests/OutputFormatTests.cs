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

using System.IO;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Tests for output format selection (XML vs JSON) and filename control.
    /// </summary>
    [Collection("E2E")]
    public sealed class OutputFormatTests
    {
        private readonly E2EFixture _fixture;

        public OutputFormatTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DefaultOutput_IsXml()
        {
            using var solution = await new SolutionBuilder("DefaultFmtSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.True(File.Exists(Path.Combine(outputDir.Path, "bom.xml")),
                "Expected bom.xml to be created by default");
            Assert.StartsWith("<?xml", result.BomContent.TrimStart());
        }

        [Fact]
        public async Task JsonFormat_ProducesJsonFile()
        {
            using var solution = await new SolutionBuilder("JsonFmtSln")
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
                    OutputFilename = "bom.json",
                    OutputFormat = "json"
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.True(File.Exists(result.OutputFilePath),
                $"Expected JSON BOM file at: {result.OutputFilePath}");
            Assert.StartsWith("{", result.BomContent.TrimStart());
        }

        [Fact]
        public async Task CustomFilename_CreatesCorrectFile()
        {
            using var solution = await new SolutionBuilder("CustomFilenameSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            const string customName = "sbom.xml";

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    OutputFilename = customName
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.True(File.Exists(Path.Combine(outputDir.Path, customName)),
                $"Expected output file '{customName}'");
        }

        [Fact]
        public async Task JsonFormatAutoDetected_FromFilenameExtension()
        {
            // When output filename ends in .json, format should be inferred as JSON
            using var solution = await new SolutionBuilder("AutoJsonFmtSln")
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
                    OutputFilename = "bom.json"
                    // no explicit OutputFormat — auto-detect from filename
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.NotNull(result.BomContent);
            Assert.StartsWith("{", result.BomContent.TrimStart());
        }
    }
}
