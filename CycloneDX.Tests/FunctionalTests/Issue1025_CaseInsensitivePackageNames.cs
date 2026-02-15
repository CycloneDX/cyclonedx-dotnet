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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Functional tests for issue #1025: Dependencies are dropped on letter case mismatches between referenced and defined NuGets.
    /// NuGet package IDs are case-insensitive according to the spec, so "Newtonsoft.Json" and "NewtonSoft.Json" should be treated as the same package.
    /// </summary>
    public class Issue1025_CaseInsensitivePackageNames
    {
        [Fact(Timeout = 15000)]
        public async Task CorrectCase_ShouldIncludeAllDependencies()
        {
            // Arrange - Assets file with correct casing from .csproj with proper package names
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "Issue1025-CaseInsensitivePackageNames", "correct-case.assets.json"));
            var options = new RunOptions
            {
            };

            // Act
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // Assert - Should have packages including transitive dependencies
            Assert.True(bom.Components.Count >= 2, $"Expected at least 2 packages, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => 
                string.Equals(c.Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase) && c.Version == "13.0.3");
            Assert.Contains(bom.Components, c => 
                string.Equals(c.Name, "System.Text.Json", StringComparison.OrdinalIgnoreCase) && c.Version == "8.0.0");
        }

        [Fact(Timeout = 15000)]
        public async Task CaseMismatch_ShouldStillIncludeAllDependencies()
        {
            // Arrange - Assets file from .csproj with case mismatches in PackageReference
            // e.g., <PackageReference Include="NewtonSoft.Json"> instead of "Newtonsoft.Json"
            // NuGet normalizes the package name but preserves the mismatch in projectFileDependencyGroups
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "Issue1025-CaseInsensitivePackageNames", "case-mismatch.assets.json"));
            var options = new RunOptions
            {
            };

            // Act
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // Assert - Should STILL have all packages even with case mismatch in .csproj
            // This test may FAIL if the tool doesn't handle case-insensitive matching properly
            Assert.True(bom.Components.Count >= 2, $"Expected at least 2 packages, got {bom.Components.Count}");
            
            // Verify Newtonsoft.Json is present (referenced as "NewtonSoft.Json" in .csproj)
            Assert.Contains(bom.Components, c => 
                string.Equals(c.Name, "Newtonsoft.Json", StringComparison.OrdinalIgnoreCase) && c.Version == "13.0.3");
            
            // Verify System.Text.Json is present (referenced as "SYSTEM.TEXT.JSON" in .csproj)
            Assert.Contains(bom.Components, c => 
                string.Equals(c.Name, "System.Text.Json", StringComparison.OrdinalIgnoreCase) && c.Version == "8.0.0");
        }

        [Fact(Timeout = 15000)]
        public async Task CaseMismatch_ShouldHaveCorrectDependencyGraph()
        {
            // Arrange - Assets from .csproj with case mismatches
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "Issue1025-CaseInsensitivePackageNames", "case-mismatch.assets.json"));
            var options = new RunOptions
            {
            };

            // Act
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // Assert - Verify dependency relationships are maintained despite case differences
            // System.Text.Json has dependencies on other packages that should be preserved
            var systemTextJsonComponent = bom.Components
                .FirstOrDefault(c => string.Equals(c.Name, "System.Text.Json", StringComparison.OrdinalIgnoreCase));
            
            var systemTextJsonRef = systemTextJsonComponent?.BomRef;

            Assert.NotNull(systemTextJsonRef);
            
            // Verify that System.Text.Json is in the dependency graph
            FunctionalTestHelper.AssertHasDependency(
                bom, 
                systemTextJsonRef,
                "System.Text.Json should be present in dependency graph even when referenced as SYSTEM.TEXT.JSON in .csproj");
        }
    }
}
