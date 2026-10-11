// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Regression test for issue #1126:
    /// --exclude-filter removed every package for projects targeting e.g. net48.
    ///
    /// Direct references were detected by comparing LockFileTarget.Name with the raw keys of
    /// "projectFileDependencyGroups" as strings. NuGet's reader may normalize the target name
    /// (e.g. "net48" became ".NETFramework,Version=v4.8"), so no package was marked as a direct
    /// reference, and the orphan removal that follows the exclude filter removed everything.
    /// </summary>
    public class Issue1126_ExcludeFilterNonNormalizedFramework
    {
        private static readonly string AssetsFile =
            Path.Combine("FunctionalTests", "Issue1126-ExcludeFilterNonNormalizedFramework", "net48.assets.json");

        [Fact]
        public async Task ExcludeFilterForMissingPackage_KeepsAllPackages()
        {
            var assetsJson = await File.ReadAllTextAsync(AssetsFile);
            var options = new RunOptions
            {
                DependencyExcludeFilter = "PackageThatDoesNotExist",
                outputFormat = OutputFileFormat.Json
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Equal(10, bom.Components.Count);
            Assert.Contains(bom.Components, c => c.Name == "Newtonsoft.Json" && c.Version == "13.0.3");
            Assert.Contains(bom.Components, c => c.Name == "Serilog.Sinks.Console" && c.Version == "6.0.0");
            Assert.Contains(bom.Components, c => c.Name == "Serilog" && c.Version == "4.0.0");
        }

        [Fact]
        public async Task ExcludeFilter_RemovesOnlyExcludedPackageAndItsOrphans()
        {
            var assetsJson = await File.ReadAllTextAsync(AssetsFile);
            var options = new RunOptions
            {
                DependencyExcludeFilter = "Serilog.Sinks.Console",
                outputFormat = OutputFileFormat.Json
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            var component = Assert.Single(bom.Components);
            Assert.Equal("Newtonsoft.Json", component.Name);
        }
    }
}
