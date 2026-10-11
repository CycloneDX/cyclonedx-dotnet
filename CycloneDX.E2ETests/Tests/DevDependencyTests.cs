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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    /// <summary>
    /// Tests that package assets determine runtime use independently of PrivateAssets.
    /// Development-only dependencies are included with excluded scope by default,
    /// but the deprecated --exclude-dev option still filters them and test-project-only packages.
    /// </summary>
    [Collection("E2E")]
    public sealed class DevDependencyTests
    {
        private readonly E2EFixture _fixture;

        public DevDependencyTests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DevDependency_IncludedWithScopeExcluded()
        {
            // Dev dependencies must appear in <components> with <scope>excluded</scope>
            using var solution = await new SolutionBuilder("DevDepDefaultSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.BuildOnly", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnly"));
            // No formulation — dev deps stay in components
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Fact]
        public async Task DevDependency_ScopeExcluded_RuntimePackage_ScopeRequired()
        {
            // Runtime packages must be scope=required; dev deps scope=excluded
            using var solution = await new SolutionBuilder("DevDepScopeSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.BuildOnly", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");

            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnly"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RuntimePackage_WithPrivateAssetsAll_IsRequired(bool excludeDev)
        {
            using var solution = await new SolutionBuilder("PrivateRuntimeAllSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            Assert.True(PackageHasRuntimeAsset(solution, "MyApp", "TestPkg.A"));

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = excludeDev });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
        }

        [Fact]
        public async Task RuntimePackage_WithPartialPrivateAssets_IsRequired()
        {
            using var solution = await new SolutionBuilder("PrivateRuntimePartialSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0", privateAssets: "contentFiles"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            Assert.True(PackageHasRuntimeAsset(solution, "MyApp", "TestPkg.A"));

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
        }

        [Fact]
        public async Task ExcludeDevFlag_FiltersAndWarnsThatItIsDeprecated()
        {
            using var solution = await new SolutionBuilder("DevDepFlagSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.BuildOnly", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeDev = true
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.DoesNotContain("TestPkg.BuildOnly", result.BomContent);
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            Assert.Contains("deprecated", result.StdOut, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("still filters", result.StdOut, System.StringComparison.OrdinalIgnoreCase);
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TransitiveOfDevDependency_FilteredOnlyWithExcludeDev(bool excludeDev)
        {
            using var solution = await new SolutionBuilder("TransitiveDevDepSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.BuildOnlyWithDep", "1.0.0", devDependency: true))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = excludeDev });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            if (excludeDev)
            {
                Assert.DoesNotContain("TestPkg.BuildOnlyWithDep", result.BomContent);
                Assert.DoesNotContain("TestPkg.DevTransitive", result.BomContent);
            }
            else
            {
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnlyWithDep"));
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.DevTransitive"));
            }
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TransitiveDependency_SharedWithRuntimeDependency_IsRequired(bool excludeDev)
        {
            using var solution = await new SolutionBuilder("SharedTransitiveDevDepSln")
                .AddProject("MyApp", p => p
                    .WithTargetFramework("net8.0")
                    .AddPackage("TestPkg.BuildOnlyWithDep", "1.0.0", devDependency: true)
                    .AddPackage("TestPkg.RuntimeWithDep", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = excludeDev });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            if (excludeDev)
                Assert.DoesNotContain("TestPkg.BuildOnlyWithDep", result.BomContent);
            else
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnlyWithDep"));
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.RuntimeWithDep"));
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.DevTransitive"));
            var document = XDocument.Parse(result.BomContent);
            var ns = document.Root!.Name.Namespace;
            var runtimeRef = document.Descendants(ns + "component")
                .Single(c => c.Element(ns + "name")?.Value == "TestPkg.RuntimeWithDep").Attribute("bom-ref")!.Value;
            var sharedRef = document.Descendants(ns + "component")
                .Single(c => c.Element(ns + "name")?.Value == "TestPkg.DevTransitive").Attribute("bom-ref")!.Value;
            Assert.Contains(document.Descendants(ns + "dependency"), d =>
                d.Attribute("ref")?.Value == runtimeRef &&
                d.Elements(ns + "dependency").Any(child => child.Attribute("ref")?.Value == sharedRef));
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExcludeDev_SharedPackageRuntimeUseWinsRegardlessOfProjectOrder(bool runtimeFirst)
        {
            var builder = new SolutionBuilder("SharedPackageOrderSln");
            var names = runtimeFirst ? new[] { "Runtime", "Build" } : new[] { "Build", "Runtime" };
            foreach (var name in names)
            {
                builder.AddProject(name, p =>
                {
                    if (name == "Runtime")
                        p.AddPackage("TestPkg.A", "1.0.0");
                    else
                        p.WithRawXml("""
                            <ItemGroup>
                              <PackageReference Include="TestPkg.A" Version="1.0.0" ExcludeAssets="all" />
                            </ItemGroup>
                            """);
                });
            }
            using var solution = await builder.BuildAsync(_fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();
            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = true });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task TestProject_OnlyPackagesAreOmittedByEitherExclusionFlag_ProductionSharedRetained(bool excludeDev, bool excludeTestProjects)
        {
            using var solution = await new SolutionBuilder("TestAndDevInteractionSln")
                .AddProject("Tests", p => p.AsTestProject()
                    .AddPackage("TestPkg.RuntimeWithDep", "1.0.0")
                    .AddPackage("TestPkg.A", "1.0.0")
                    .AddPackage("TestPkg.BuildOnly", "1.0.0"))
                .AddProject("App", p => p.AddPackage("TestPkg.A", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();
            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    ExcludeDev = excludeDev,
                    ExcludeTestProjects = excludeTestProjects
                });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            if (excludeDev || excludeTestProjects)
            {
                Assert.DoesNotContain("TestPkg.RuntimeWithDep", result.BomContent);
                Assert.DoesNotContain("TestPkg.DevTransitive", result.BomContent);
                Assert.DoesNotContain("TestPkg.BuildOnly", result.BomContent);
            }
            else
            {
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.RuntimeWithDep"));
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.DevTransitive"));
                Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnly"));
            }
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
        }

        [Fact]
        public async Task BuildOnlyPackage_WithDefaultPrivateAssets_IsExcluded()
        {
            using var solution = await new SolutionBuilder("DefaultPrivateBuildOnlySln")
                .AddProject("App", p => p.AddPackage("TestPkg.BuildOnly", "1.0.0"))
                .BuildAsync(_fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.BuildOnly"));
        }

        [Fact]
        public async Task MixedAssetPackage_WithOnlyBuildAssetsSelected_IsExcluded()
        {
            const string packageId = "TestPkg.MixedBuildAndRuntime";
            using var packageStream = new MemoryStream();
            // The builder supplies lib assemblies; add a build target to make a mixed-asset
            // package, then select only its build assets in the project below.
            packageStream.Write(NupkgBuilder.Build(packageId, "1.0.0"));
            using (var archive = new System.IO.Compression.ZipArchive(packageStream,
                System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
            {
                var entry = archive.CreateEntry($"build/{packageId}.targets");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("<Project />");
            }
            await _fixture.PushPackageAsync(packageStream.ToArray());

            using var solution = await new SolutionBuilder("OnlyBuildAssetsSelectedSln")
                .AddProject("App", p => p.WithRawXml($"""
                    <ItemGroup>
                      <PackageReference Include="{packageId}" Version="1.0.0"
                                        PrivateAssets="all" IncludeAssets="build" />
                    </ItemGroup>
                    """))
                .BuildAsync(_fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("excluded", GetComponentScope(result.BomContent, packageId));
        }

        [Fact]
        public async Task Package_WithAllAssetsExcluded_IsExcluded()
        {
            using var solution = await new SolutionBuilder("AllAssetsExcludedSln")
                .AddProject("App", p => p.WithRawXml("""
                    <ItemGroup>
                      <PackageReference Include="TestPkg.A" Version="1.0.0" ExcludeAssets="all" />
                    </ItemGroup>
                    """))
                .BuildAsync(_fixture.NuGetFeedUrl);
            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("excluded", GetComponentScope(result.BomContent, "TestPkg.A"));
        }

        private static void AssertDependencyGraphHasNoDanglingReferences(string bomContent)
        {
            var document = XDocument.Parse(bomContent);
            var ns = document.Root!.Name.Namespace;
            var refs = document.Descendants(ns + "component")
                .Select(c => c.Attribute("bom-ref")?.Value).Where(r => r != null).ToHashSet();
            Assert.All(document.Descendants(ns + "dependency"), d =>
                Assert.Contains(d.Attribute("ref")!.Value, refs));
        }

        private static string GetComponentScope(string bomContent, string componentName)
        {
            var document = XDocument.Parse(bomContent);
            var ns = document.Root!.Name.Namespace;
            var component = document.Descendants(ns + "component")
                .Single(c => c.Element(ns + "name")?.Value == componentName);

            return component.Element(ns + "scope")!.Value;
        }

        private static bool PackageHasRuntimeAsset(
            BuiltSolution solution,
            string projectName,
            string packageName)
        {
            var assetsPath = Path.Combine(solution.RootDir, projectName, "obj", "project.assets.json");
            using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));

            return document.RootElement.GetProperty("targets")
                .EnumerateObject()
                .SelectMany(target => target.Value.EnumerateObject())
                .Where(package => package.Name.StartsWith(packageName + "/", System.StringComparison.OrdinalIgnoreCase))
                .Any(package => package.Value.TryGetProperty("runtime", out var runtime) &&
                    runtime.EnumerateObject().Any());
        }
    }
}
