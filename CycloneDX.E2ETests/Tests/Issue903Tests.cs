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

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Regression tests for issue #903:
    /// "Unable to locate valid bom ref for &lt;package&gt; [x.y.z, x.y.z]" when scanning a
    /// solution that contains multiple projects referencing the same package at different versions.
    ///
    /// Root cause: NuGet stores a dependency's version constraint verbatim from the .nuspec into
    /// project.assets.json. When a package declares its dep with exact-range notation
    /// (e.g. "[1.0.0, 1.0.0]"), the version string stored in the lock file is a range, not a
    /// plain version. Runner.cs builds its bomRefLookup keyed on plain versions only, so the
    /// range string misses on the first lookup attempt. The name-only fallback at that point
    /// succeeds only when exactly one version of the package is present in the BOM — but in a
    /// multi-project solution a second version of the same package may be directly referenced,
    /// giving two candidates and causing the error.
    ///
    /// The fix must resolve the range "[1.0.0, 1.0.0]" to the concrete version "1.0.0" and
    /// look that up successfully even when another project directly pins "2.0.0".
    /// </summary>
    [Collection("E2E")]
    public sealed class Issue903Tests
    {
        private readonly E2EFixture _fixture;

        public Issue903Tests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VersionRangeDependency_MultipleVersionsInSolution_ShouldSucceed()
        {
            // Reproduces the exact topology reported in PR #903 / issue comment by @ilehtoranta:
            //
            //   ProjectB  — directly references TestPkg.Shared 1.0.0
            //   ProjectA  — ProjectReference to ProjectB
            //             — directly references TestPkg.Shared 2.0.0
            //             — directly references TestPkg.Consumer 1.0.0
            //   TestPkg.Consumer — depends on TestPkg.Shared [1.0.0, 1.0.0]  (exact-range notation)
            //
            // After restore:
            //   ProjectA's assets: NuGet resolves TestPkg.Shared to 2.0.0 (direct ref wins).
            //     TestPkg.Consumer's dep is stored as "[1.0.0]" in project.assets.json.
            //     ResolveDependencyVersionRanges cannot resolve it because 2.0.0 does not
            //     satisfy the exact range [1.0.0]. Range string is left unresolved.
            //   ProjectB's assets: resolves TestPkg.Shared/1.0.0.
            //
            // The tool merges both projects' packages into one BOM → both Shared 1.0.0 and 2.0.0
            // are present. The name-only fallback in Runner.cs finds two candidates → crash.
            //
            // Before the fix: "Unable to locate valid bom ref for TestPkg.Shared [1.0.0, 1.0.0]"
            // After the fix:  tool succeeds and both versions appear in the BOM.
            using var solution = await new SolutionBuilder("Issue903Sln")
                .AddProject("ProjectB", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Shared", "1.0.0"))
                .AddProject("ProjectA", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Shared", "2.0.0")
                    .AddPackage("TestPkg.Consumer", "1.0.0")
                    .AddProjectReference("../ProjectB/ProjectB.csproj"))
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

            Assert.True(result.Success,
                $"Tool failed with exit code {result.ExitCode}.\nstderr:\n{result.StdErr}\nstdout:\n{result.StdOut}");

            // Both versions of the shared package must appear as distinct components in the BOM.
            // Scanning a solution is an explicit union of all projects — duplicate versions of the
            // same package across projects are expected and correct.
            Assert.Contains("pkg:nuget/TestPkg.Shared@1.0.0", result.BomContent);
            Assert.Contains("pkg:nuget/TestPkg.Shared@2.0.0", result.BomContent);
            Assert.Contains("pkg:nuget/TestPkg.Consumer@1.0.0", result.BomContent);

            // The critical correctness check: TestPkg.Consumer's dependency edge must point to
            // TestPkg.Shared 1.0.0 (what its nuspec declares), not 2.0.0.
            // In the XML the dependencies section looks like:
            //   <dependency ref="pkg:nuget/TestPkg.Consumer@1.0.0">
            //     <dependency ref="pkg:nuget/TestPkg.Shared@1.0.0"/>
            //   </dependency>
            var consumerDepBlock = Regex.Match(
                result.BomContent,
                @"<dependency ref=""pkg:nuget/TestPkg\.Consumer@1\.0\.0"">(.*?)</dependency>",
                RegexOptions.Singleline).Value;

            Assert.False(string.IsNullOrEmpty(consumerDepBlock),
                "No dependency block found for TestPkg.Consumer@1.0.0");
            Assert.Contains("pkg:nuget/TestPkg.Shared@1.0.0", consumerDepBlock);
            Assert.DoesNotContain("pkg:nuget/TestPkg.Shared@2.0.0", consumerDepBlock);
        }
    }
}
