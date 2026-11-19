// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    [Collection("E2E")]
    public sealed class Issue1147Tests(E2EFixture fixture)
    {
        [Theory]
        [InlineData("net11.0", false)]
        [InlineData("net11.0-windows", false)]
        [InlineData("net11.0-windows", true)]
        public async Task RuntimePackageFromProjectReference_IsRequired(string appFramework, bool excludeDev)
        {
            // The app has no PackageReference of its own. Let the SDK write the assets
            // for the Windows target and the reference to a platform-neutral library.
            using var solution = await new SolutionBuilder("Issue1147")
                .AddProject("Library", p => p.WithTargetFramework("net11.0").AddPackage("TestPkg.C", "1.0.0"))
                .AddProject("App", p => p.WithTargetFramework(appFramework)
                    .AddProjectReference("../Library/Library.csproj"))
                .BuildAsync(fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();

            var result = await fixture.Runner.RunAsync(
                solution.ProjectFile("App"), outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = fixture.NuGetFeedUrl,
                    OutputFormat = "json",
                    ExcludeDev = excludeDev
                });

            Assert.True(result.Success, $"Tool failed with exit code {result.ExitCode}.\n{result.StdErr}\n{result.StdOut}");
            Assert.NotNull(result.BomContent);
            using var bom = JsonDocument.Parse(result.BomContent);
            var root = bom.RootElement;
            var package = Assert.Single(root.GetProperty("components").EnumerateArray(),
                component => component.GetProperty("name").GetString() == "TestPkg.C");
            Assert.Equal("required", package.GetProperty("scope").GetString());

            var appRef = root.GetProperty("metadata").GetProperty("component").GetProperty("bom-ref").GetString();
            var app = Assert.Single(root.GetProperty("dependencies").EnumerateArray(),
                dependency => dependency.GetProperty("ref").GetString() == appRef);
            Assert.Contains(package.GetProperty("bom-ref").GetString(),
                app.GetProperty("dependsOn").EnumerateArray().Select(reference => reference.GetString()));
        }
    }
}
