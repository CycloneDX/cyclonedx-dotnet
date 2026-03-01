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
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Regression test for issue #1025:
    /// Dependencies are dropped when a package's .nuspec lists a dependency with different casing
    /// than the canonical package ID used in the lock file's libraries/targets section.
    ///
    /// NuGet package IDs are case-insensitive, but project.assets.json preserves the casing
    /// from each package's own .nuspec when writing the "dependencies" dict inside "targets".
    /// If BadlyDesigned.Package's .nuspec declares its dep as "newtonsoft.Json" (wrong casing)
    /// but the canonical library key is "Newtonsoft.Json", then in Runner.cs the line:
    ///
    ///   var dependenciesWithoutPackages = allDependencies.Except(packages.Select(p => p.Name))
    ///
    /// treats "newtonsoft.Json" as having no matching package (because Except uses ordinal
    /// string comparison), and removes it from the dependency graph unconditionally on every run.
    /// This regression was introduced in the fix for issue #894.
    /// </summary>
    public class Issue1025_CaseInsensitivePackageNames
    {
        [Fact]
        public async Task TransitiveDependency_ShouldNotBeDropped_WhenDepNuspecHasCaseMismatch()
        {
            // case-mismatch.assets.json simulates a real-world scenario where BadlyDesigned.Package
            // was published with its .nuspec listing "newtonsoft.Json" (wrong casing) as a dependency,
            // while the canonical lock-file key is "Newtonsoft.Json".
            // NuGet copies the .nuspec casing verbatim into the targets->dependencies dict.
            var assetContents = await File.ReadAllTextAsync(
                Path.Combine("FunctionalTests", "Issue1025-CaseInsensitivePackageNames", "case-mismatch.assets.json"));

            // No --exclude filter needed: the bug fires unconditionally via the
            // dependenciesWithoutPackages Except() check added in the #894 fix.
            var options = new RunOptions { outputFormat = OutputFileFormat.Json };
            var bom = await FunctionalTestHelper.Test(assetContents, options);

            // BadlyDesigned.Package is the direct dep; Newtonsoft.Json is its transitive dep.
            // Both must appear as components in the SBOM.
            Assert.Equal(2, bom.Components.Count);
            Assert.Contains(bom.Components, c => c.Name == "BadlyDesigned.Package" && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => c.Name == "Newtonsoft.Json" && c.Version == "13.0.1");

            // More importantly: the dependency graph must show BadlyDesigned.Package -> Newtonsoft.Json.
            // The bug (Runner.cs:254) removes "newtonsoft.Json" from the Dependencies dict because
            // the case-insensitive Except() treats it as having no matching package in the packages set.
            // This causes Newtonsoft.Json to be missing from BadlyDesigned.Package's dependency list.
            var badlyDesignedBomRef = bom.Components.First(c => c.Name == "BadlyDesigned.Package").BomRef;
            var newtonsoftBomRef = bom.Components.First(c => c.Name == "Newtonsoft.Json").BomRef;
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, badlyDesignedBomRef, newtonsoftBomRef,
                "Newtonsoft.Json should appear as a dependency of BadlyDesigned.Package in the dependency graph. " +
                "If this fails, the case-mismatch bug (issue #1025) is present: the dep was silently dropped from " +
                "the graph because 'newtonsoft.Json' != 'Newtonsoft.Json' under ordinal comparison.");
        }
    }
}
