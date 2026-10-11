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
    /// Regression tests for issue #1110: solution scanning must not fail when multiple
    /// resolved package versions satisfy an unresolved dependency version range.
    /// </summary>
    [Collection("E2E")]
    public sealed class Issue1110Tests
    {
        private readonly E2EFixture _fixture;

        public Issue1110Tests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DependencyRange_WithMultipleMatchingVersionsInSolution_ShouldSucceed()
        {
            await _fixture.PushPackageAsync(NupkgBuilder.Build("TestPkg.Issue1110.Logging", "1.0.3"));
            await _fixture.PushPackageAsync(NupkgBuilder.Build("TestPkg.Issue1110.Logging", "1.0.9"));
            await _fixture.PushPackageAsync(NupkgBuilder.Build("TestPkg.Issue1110.Logging", "1.0.10"));
            await _fixture.PushPackageAsync(NupkgBuilder.Build(
                "TestPkg.Issue1110.Range", "1.0.0",
                dependencies: new[] { new NupkgDependency("TestPkg.Issue1110.Logging", "[1.0.9, )") }));

            // RangeProducer represents the project used to create the package. ConsumerA keeps
            // the range unresolved by directly pinning a lower version, while ConsumerB resolves
            // another version that also satisfies the package's range.
            using var solution = await new SolutionBuilder("Issue1110Sln")
                .AddProject("RangeProducer", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Issue1110.Logging", "1.0.9"))
                .AddProject("ConsumerA", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Issue1110.Range", "1.0.0")
                    .AddPackage("TestPkg.Issue1110.Logging", "1.0.3")
                    .WithRawXml("""
                        <PropertyGroup>
                          <NoWarn>$(NoWarn);NU1605</NoWarn>
                        </PropertyGroup>
                        """))
                .AddProject("ConsumerB", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.Issue1110.Logging", "1.0.10"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    NoSerialNumber = true,
                    DisableHashComputation = true
                });

            Assert.True(result.Success,
                $"Tool failed with exit code {result.ExitCode}.\nstderr:\n{result.StdErr}\nstdout:\n{result.StdOut}");

            await Verify(result.BomContent);
        }
    }
}
