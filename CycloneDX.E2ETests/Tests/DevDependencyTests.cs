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
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task TransitiveOfDevDependency_FilteredOnlyWithExcludeDev(bool excludeDev, bool restricted)
        {
            // devDependency supplies only PrivateAssets=all: propagation is not development
            // intent. Only an explicit build-only selection excludes this runtime-bearing closure.
            using var solution = await new SolutionBuilder("TransitiveDevDepSln")
                .AddProject("MyApp", p =>
                {
                    p.WithTargetFramework("net8.0").AddPackage("TestPkg.A", "1.0.0");
                    if (restricted)
                        p.WithRawXml("""
                            <ItemGroup>
                              <PackageReference Include="TestPkg.BuildOnlyWithDep" Version="1.0.0"
                                                PrivateAssets="all" IncludeAssets="build;buildTransitive" />
                            </ItemGroup>
                            """);
                    else
                        p.AddPackage("TestPkg.BuildOnlyWithDep", "1.0.0", devDependency: true);
                })
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = excludeDev });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            Assert.Equal("required", GetComponentScope(result.BomContent, "TestPkg.A"));
            if (excludeDev && restricted)
            {
                Assert.DoesNotContain("TestPkg.BuildOnlyWithDep", result.BomContent);
                Assert.DoesNotContain("TestPkg.DevTransitive", result.BomContent);
            }
            else
            {
                Assert.Equal(restricted ? "excluded" : "required", GetComponentScope(result.BomContent, "TestPkg.BuildOnlyWithDep"));
                Assert.Equal(restricted ? "excluded" : "required", GetComponentScope(result.BomContent, "TestPkg.DevTransitive"));
                AssertDependencyEdge(result.BomContent, "TestPkg.BuildOnlyWithDep", "TestPkg.DevTransitive");
            }
            AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
            Assert.DoesNotContain("<formulation>", result.BomContent);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task TransitiveDependency_SharedWithRuntimeDependency_IsRequired(bool excludeDev, bool restricted)
        {
            using var solution = await new SolutionBuilder("SharedTransitiveDevDepSln")
                .AddProject("MyApp", p =>
                {
                    p.WithTargetFramework("net8.0").AddPackage("TestPkg.RuntimeWithDep", "1.0.0");
                    // PrivateAssets alone retains the ambiguous wrapper; explicit selection
                    // preserves genuinely development-only filtering even with a shared child.
                    if (restricted)
                        p.WithRawXml("""
                            <ItemGroup>
                              <PackageReference Include="TestPkg.BuildOnlyWithDep" Version="1.0.0"
                                                PrivateAssets="all" IncludeAssets="build;buildTransitive" />
                            </ItemGroup>
                            """);
                    else
                        p.AddPackage("TestPkg.BuildOnlyWithDep", "1.0.0", devDependency: true);
                })
                .BuildAsync(_fixture.NuGetFeedUrl);

            using var outputDir = solution.CreateOutputDir();

            var result = await _fixture.Runner.RunAsync(
                solution.SolutionFile,
                outputDir.Path,
                new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, ExcludeDev = excludeDev });

            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}");
            if (excludeDev && restricted)
                Assert.DoesNotContain("TestPkg.BuildOnlyWithDep", result.BomContent);
            else
            {
                Assert.Equal(restricted ? "excluded" : "required", GetComponentScope(result.BomContent, "TestPkg.BuildOnlyWithDep"));
                AssertDependencyEdge(result.BomContent, "TestPkg.BuildOnlyWithDep", "TestPkg.DevTransitive");
            }
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

        [Theory]
        [InlineData("xml", false)]
        [InlineData("xml", true)]
        [InlineData("json", false)]
        [InlineData("json", true)]
        public async Task RuntimeMetapackage_RestoredClosureRespectsSelectionAndTestUse(string format, bool excludeDev)
        {
            // Unique IDs prevent global NuGet cache/feed collisions. These tiny payloads prove
            // restore inventories and CLI classification, not executable or publish contents.
            var parent = "TestPkg.RuntimeWrapper." + System.Guid.NewGuid().ToString("N");
            var managed = parent + ".Managed";
            var transitive = parent + ".Transitive";
            var native = parent + ".Native";
            await _fixture.PushPackageAsync(NupkgBuilder.Build(transitive, "1.0.0"));
            await _fixture.PushPackageAsync(NupkgBuilder.Build(managed, "1.0.0",
                dependencies: new[] { new NupkgDependency(transitive, "1.0.0") }));

            using var nativeStream = new MemoryStream();
            nativeStream.Write(NupkgBuilder.Build(native, "1.0.0", buildOnly: true));
            using (var archive = new System.IO.Compression.ZipArchive(nativeStream,
                System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
            {
                archive.GetEntry($"build/{native}.targets")!.Delete();
                using var writer = new StreamWriter(archive.CreateEntry($"runtimes/linux-x64/native/lib{native}.so").Open());
                writer.Write("synthetic native payload");
            }
            await _fixture.PushPackageAsync(nativeStream.ToArray());

            using var parentStream = new MemoryStream();
            parentStream.Write(NupkgBuilder.Build(parent, "1.0.0", buildOnly: true,
                dependencies: new[] { new NupkgDependency(managed, "1.0.0"), new NupkgDependency(native, "1.0.0") }));
            using (var archive = new System.IO.Compression.ZipArchive(parentStream,
                System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
            {
                archive.GetEntry($"build/{parent}.targets")!.Delete();
                using var writer = new StreamWriter(archive.CreateEntry($"buildTransitive/{parent}.targets").Open());
                writer.Write("<Project />");
            }
            await _fixture.PushPackageAsync(parentStream.ToArray());

            foreach (var use in new[] { "Production", "Restricted", "TestOnly", "Shared" })
            {
                var builder = new SolutionBuilder("RuntimeWrapper" + use).AddProject("Consumer", p =>
                {
                    if (use == "TestOnly" || use == "Shared")
                        p.AsTestProject();
                    if (use == "Restricted")
                        p.WithRawXml($"""
                            <ItemGroup>
                              <PackageReference Include="{parent}" Version="1.0.0"
                                                IncludeAssets="build;buildTransitive" />
                            </ItemGroup>
                            """);
                    else
                        p.AddPackage(parent, "1.0.0");
                });
                if (use == "Shared")
                    builder.AddProject("Production", p => p.AddPackage(parent, "1.0.0"));
                using var solution = await builder.BuildAsync(_fixture.NuGetFeedUrl);

                using var assets = JsonDocument.Parse(File.ReadAllText(
                    Path.Combine(solution.RootDir, "Consumer", "obj", "project.assets.json")));
                var target = assets.RootElement.GetProperty("targets").GetProperty("net8.0");
                var wrapper = target.GetProperty(parent + "/1.0.0");
                Assert.Equal(new[] { $"buildTransitive/{parent}.targets" },
                    wrapper.GetProperty("build").EnumerateObject().Select(a => a.Name).ToArray());
                foreach (var category in new[] { "compile", "runtime", "runtimeTargets", "native", "resource", "contentFiles", "frameworkAssemblies", "embed" })
                    Assert.False(wrapper.TryGetProperty(category, out var ownAssets) && ownAssets.EnumerateObject().Any(),
                        $"Wrapper unexpectedly has own {category} assets");
                Assert.Equal(new[] { managed, native }, wrapper.GetProperty("dependencies").EnumerateObject().Select(d => d.Name).OrderBy(n => n).ToArray());
                Assert.Equal(new[] { transitive }, target.GetProperty(managed + "/1.0.0")
                    .GetProperty("dependencies").EnumerateObject().Select(d => d.Name).ToArray());
                if (use != "Restricted")
                {
                    foreach (var dllPackage in new[] { managed, transitive })
                        Assert.Contains(target.GetProperty(dllPackage + "/1.0.0").GetProperty("runtime").EnumerateObject(),
                            a => a.Name == $"lib/net8.0/{dllPackage}.dll");
                    var nativeAsset = target.GetProperty(native + "/1.0.0").GetProperty("runtimeTargets")
                        .GetProperty($"runtimes/linux-x64/native/lib{native}.so");
                    Assert.Equal("native", nativeAsset.GetProperty("assetType").GetString());
                    Assert.Equal("linux-x64", nativeAsset.GetProperty("rid").GetString());
                }

                using var outputDir = solution.CreateOutputDir();
                var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                    new ToolRunOptions { NuGetFeedUrl = _fixture.NuGetFeedUrl, OutputFormat = format, ExcludeDev = excludeDev });
                Assert.True(result.Success, $"{use}: Tool failed:\n{result.StdErr}");
                var required = use == "Production" || use == "Shared";
                foreach (var package in new[] { parent, managed, transitive, native })
                {
                    if (excludeDev && !required)
                        Assert.DoesNotContain(package, result.BomContent);
                    else
                        Assert.Equal(required ? "required" : "excluded", GetComponentScope(result.BomContent, package));
                }
                if (!excludeDev || required)
                {
                    AssertDependencyEdge(result.BomContent, parent, managed);
                    AssertDependencyEdge(result.BomContent, parent, native);
                    AssertDependencyEdge(result.BomContent, managed, transitive);
                }
                AssertDependencyGraphHasNoDanglingReferences(result.BomContent);
            }
        }

        // Normalize only the component/edge fields used by assertions; no snapshot or
        // serialization comparison can mask a missing component, scope, or dependency edge.
        private static XDocument ReadBomGraph(string bomContent)
        {
            if (bomContent.TrimStart().StartsWith("<", System.StringComparison.Ordinal))
                return XDocument.Parse(bomContent);
            using var json = JsonDocument.Parse(bomContent);
            XElement Component(JsonElement c) => new XElement("component",
                new XAttribute("bom-ref", c.GetProperty("bom-ref").GetString()!),
                new XElement("name", c.GetProperty("name").GetString()),
                new XElement("scope", c.TryGetProperty("scope", out var scope) ? scope.GetString() : null));
            var root = new XElement("bom",
                new XElement("components", json.RootElement.GetProperty("components").EnumerateArray().Select(Component)),
                new XElement("dependencies", json.RootElement.GetProperty("dependencies").EnumerateArray().Select(d =>
                    new XElement("dependency", new XAttribute("ref", d.GetProperty("ref").GetString()!),
                        d.TryGetProperty("dependsOn", out var children)
                            ? children.EnumerateArray().Select(c => new XElement("dependency", new XAttribute("ref", c.GetString()!)))
                            : Enumerable.Empty<XElement>()))));
            if (json.RootElement.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("component", out var component))
                root.Add(new XElement("metadata", Component(component)));
            return new XDocument(root);
        }

        private static void AssertDependencyEdge(string bomContent, string parent, string child)
        {
            var document = ReadBomGraph(bomContent);
            var ns = document.Root!.Name.Namespace;
            string Ref(string name) => document.Descendants(ns + "component")
                .Single(c => c.Element(ns + "name")?.Value == name).Attribute("bom-ref")!.Value;
            var parentRef = Ref(parent);
            var childRef = Ref(child);
            Assert.Contains(document.Descendants(ns + "dependency"), d =>
                d.Attribute("ref")?.Value == parentRef &&
                d.Elements(ns + "dependency").Any(c => c.Attribute("ref")?.Value == childRef));
        }

        private static void AssertDependencyGraphHasNoDanglingReferences(string bomContent)
        {
            var document = ReadBomGraph(bomContent);
            var ns = document.Root!.Name.Namespace;
            var refs = document.Descendants(ns + "component")
                .Select(c => c.Attribute("bom-ref")?.Value).Where(r => r != null).ToHashSet();
            Assert.All(document.Descendants(ns + "dependency"), d =>
                Assert.Contains(d.Attribute("ref")!.Value, refs));
        }

        private static string GetComponentScope(string bomContent, string componentName)
        {
            var document = ReadBomGraph(bomContent);
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
