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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    [Collection("E2E")]
    public sealed class Issue1143Tests
    {
        private readonly E2EFixture _fixture;

        public Issue1143Tests(E2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Theory]
        [InlineData("Json", false)]
        [InlineData("Json", true)]
        [InlineData("Xml", true)]
        public async Task PrivateDependencyMissingFromConsumer_ListsEachMatchingVersionOnce(
            string outputFormat, bool multipleVersions)
        {
            const string child = "TestPkg.Issue1143.Child";
            const string shared = "TestPkg.Issue1143.Shared";
            await _fixture.PushPackageAsync(NupkgBuilder.Build(child, "1.0.0", buildOnly: true));
            await _fixture.PushPackageAsync(NupkgBuilder.Build(shared, "1.0.0",
                dependencies: new[] { new NupkgDependency(child, "[1.0.0, )") }));

            var builder = new SolutionBuilder("Issue1143Sln")
                .AddProject("Api", p => p
                    .WithTargetFramework("net10.0")
                    .AddPackage(shared, "1.0.0")
                    .AddPackage(child, "1.0.0", privateAssets: "all"))
                .AddProject("Consumer", p => p
                    .WithTargetFramework("net10.0")
                    .AddProjectReference("../Api/Api.csproj"));

            if (multipleVersions)
            {
                // A second version satisfies Shared's range and must not be dropped.
                await _fixture.PushPackageAsync(NupkgBuilder.Build(child, "2.0.0", buildOnly: true));
                builder.AddProject("Other", p => p
                    .WithTargetFramework("net10.0")
                    .AddPackage(child, "2.0.0"));
            }

            using var solution = await builder.BuildAsync(_fixture.NuGetFeedUrl);

            // Verify the restore shape responsible for #1143: Shared retains a dependency
            // on Child in Consumer, but PrivateAssets prevents Child itself from flowing.
            using var apiAssets = JsonDocument.Parse(solution.ReadProjectAssets("Api"));
            using var consumerAssets = JsonDocument.Parse(solution.ReadProjectAssets("Consumer"));
            var apiTarget = Assert.Single(apiAssets.RootElement.GetProperty("targets").EnumerateObject()).Value;
            var consumerTarget = Assert.Single(consumerAssets.RootElement.GetProperty("targets").EnumerateObject()).Value;
            Assert.True(apiTarget.TryGetProperty($"{child}/1.0.0", out _));
            Assert.False(consumerTarget.TryGetProperty($"{child}/1.0.0", out _));
            Assert.True(consumerTarget.GetProperty($"{shared}/1.0.0")
                .GetProperty("dependencies").TryGetProperty(child, out _));

            using var outputDir = solution.CreateOutputDir();
            var result = await _fixture.Runner.RunAsync(solution.SolutionFile, outputDir.Path,
                new ToolRunOptions
                {
                    NuGetFeedUrl = _fixture.NuGetFeedUrl,
                    DisablePackageRestore = true,
                    OutputFormat = outputFormat
                });
            Assert.True(result.Success, $"Tool failed:\n{result.StdErr}\n{result.StdOut}");

            var sharedRef = $"pkg:nuget/{shared}@1.0.0";
            var expected = multipleVersions
                ? new[] { $"pkg:nuget/{child}@1.0.0", $"pkg:nuget/{child}@2.0.0" }
                : new[] { $"pkg:nuget/{child}@1.0.0" };
            var refs = outputFormat == "Json"
                ? ReadJsonDependencyRefs(result.BomContent, sharedRef)
                : ReadXmlDependencyRefs(result.BomContent, sharedRef);
            Assert.Equal(expected, refs.OrderBy(r => r, StringComparer.Ordinal).ToArray());
        }

        private static string[] ReadJsonDependencyRefs(string bomContent, string sharedRef)
        {
            using var json = JsonDocument.Parse(bomContent);
            var sharedDependency = Assert.Single(json.RootElement.GetProperty("dependencies").EnumerateArray(),
                d => d.GetProperty("ref").GetString() == sharedRef);
            return sharedDependency.GetProperty("dependsOn").EnumerateArray().Select(r => r.GetString()).ToArray();
        }

        private static string[] ReadXmlDependencyRefs(string bomContent, string sharedRef)
        {
            var root = XDocument.Parse(bomContent).Root;
            var ns = root.Name.Namespace;
            var sharedDependency = Assert.Single(root.Element(ns + "dependencies").Elements(ns + "dependency"),
                d => (string)d.Attribute("ref") == sharedRef);
            return sharedDependency.Elements(ns + "dependency").Select(r => (string)r.Attribute("ref")).ToArray();
        }
    }
}
