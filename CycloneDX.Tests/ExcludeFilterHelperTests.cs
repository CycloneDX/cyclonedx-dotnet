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

using System.Collections.Generic;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests
{
    public class ExcludeFilterHelperTests
    {
        [Theory]
        [InlineData("Child@1.0.0", 3)]
        [InlineData("Child@2.0.0", 2)]
        [InlineData("Child", 1)]
        public void ManualExclusion_TraversesAllVersionsAndRemovesOnlyOrphans(string filter, int expectedCount)
        {
            var root = new DotnetDependency { Name = "Root", Version = "1.0.0", IsDirectReference = true };
            root.AddDependencyEdge("Child", "1.0.0");
            root.AddDependencyEdge("Child", "2.0.0");
            var first = new DotnetDependency { Name = "Child", Version = "1.0.0" };
            var second = new DotnetDependency { Name = "Child", Version = "2.0.0", Scope = Component.ComponentScope.Excluded, IsDevDependency = true };
            second.AdditionalDependencyEdges.Add(new KeyValuePair<string, string>("Grandchild", "1.0.0"));
            var grandchild = new DotnetDependency { Name = "Grandchild", Version = "1.0.0" };
            var packages = new HashSet<DotnetDependency> { root, first, second, grandchild };

            ExcludeFilterHelper.ExcludePackages(packages, filter);
            ExcludeFilterHelper.RemoveOrphanedPackages(packages);

            Assert.Equal(expectedCount, packages.Count);
            Assert.Contains(root, packages);
            Assert.Equal(filter == "Child@2.0.0", packages.Contains(first));
            Assert.Equal(filter == "Child@1.0.0", packages.Contains(second));
            Assert.Equal(filter == "Child@1.0.0", packages.Contains(grandchild));
        }
    }
}
