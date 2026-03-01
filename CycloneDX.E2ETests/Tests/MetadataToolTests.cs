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
    /// Verifies that the tool records itself in BOM metadata using the non-deprecated
    /// <c>metadata/tools/components/component</c> structure (CycloneDX 1.5+), not the
    /// legacy <c>metadata/tools/tool</c> element.
    ///
    /// The snapshot captures the full <c>&lt;tools&gt;</c> block so any future change to
    /// how the tool identifies itself (name, author, URL, structure) will be visible in diff.
    /// </summary>
    [Collection("E2E")]
    public sealed class MetadataToolTests
    {
        private readonly E2EFixture _fixture;

        public MetadataToolTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ToolMetadata_IsRecordedAsComponent_NotDeprecatedTool()
        {
            using var solution = await new SolutionBuilder("ToolMetaSln")
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
            Assert.NotNull(result.BomContent);

            // Structural assertions: new format present, old format absent
            Assert.Contains("<components>", result.BomContent);
            Assert.Contains("CycloneDX module for .NET", result.BomContent);
            Assert.DoesNotContain("<tool>", result.BomContent);
            Assert.DoesNotContain("<vendor>", result.BomContent);

            // Snapshot covers the full BOM so any future structural change is visible
            await Verify(result.BomContent);
        }
    }
}
