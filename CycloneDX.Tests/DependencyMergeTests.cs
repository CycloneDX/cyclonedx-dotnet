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
using System.Linq;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests
{
    public class DependencyMergeTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Merge_RuntimeUseWinsAndAllChildVersionsAreRetained(bool reverse)
        {
            var dev = new DotnetDependency
            {
                Name = "Shared", Version = "1.0.0", IsDevDependency = true,
                IsDirectReference = true, IsRuntimeReachable = false, Scope = Component.ComponentScope.Excluded,
                Dependencies = new Dictionary<string, string> { ["Child"] = "1.0.0", ["DevChild"] = "1.0.0" }
            };
            var runtime = new DotnetDependency
            {
                Name = "Shared", Version = "1.0.0", IsRuntimeReachable = true, Scope = Component.ComponentScope.Required,
                Dependencies = new Dictionary<string, string> { ["Child"] = "2.0.0", ["RuntimeChild"] = "[3.0.0, )" }
            };
            var packages = new HashSet<DotnetDependency>();
            DotnetDependency.MergeInto(packages, reverse ? new[] { runtime, dev } : new[] { dev, runtime });
            var package = Assert.Single(packages);
            Assert.True(package.IsDirectReference);
            Assert.True(package.IsRuntimeReachable);
            Assert.False(package.IsDevDependency);
            Assert.Equal(Component.ComponentScope.Required, package.Scope);
            Assert.Equal(4, package.GetDependencyEdges().Count());
            Assert.Contains(new KeyValuePair<string, string>("Child", "1.0.0"), package.GetDependencyEdges());
            Assert.Contains(new KeyValuePair<string, string>("Child", "2.0.0"), package.GetDependencyEdges());
            Assert.Contains(new KeyValuePair<string, string>("RuntimeChild", "[3.0.0, )"), package.GetDependencyEdges());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Merge_OrdinaryTestUseIsNonDevelopmentButNotRuntime(bool reverse)
        {
            var dev = new DotnetDependency
            {
                Name = "Shared", Version = "1.0.0", IsDirectReference = true, IsDevDependency = true,
                IsNonDevelopmentReachable = false, IsRuntimeReachable = false, Scope = Component.ComponentScope.Excluded
            };
            var test = new DotnetDependency
            {
                Name = "Shared", Version = "1.0.0", IsNonDevelopmentReachable = true,
                IsRuntimeReachable = false, Scope = Component.ComponentScope.Excluded
            };
            var packages = new HashSet<DotnetDependency>();
            DotnetDependency.MergeInto(packages, reverse ? new[] { test, dev } : new[] { dev, test });
            var package = Assert.Single(packages);
            Assert.True(package.IsNonDevelopmentReachable);
            Assert.False(package.IsRuntimeReachable);
            Assert.Equal(Component.ComponentScope.Excluded, package.Scope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Merge_UnresolvedEdgeProvenanceIsUnionedSeparatelyFromGraphEdges(bool reverse)
        {
            var runtime = new DotnetDependency { Name = "Source", Version = "1.0.0" };
            var test = new DotnetDependency { Name = "Source", Version = "1.0.0" };
            var runtimeEdge = new KeyValuePair<string, string>("Child", "[1.0.0, 2.0.0)");
            var testEdge = new KeyValuePair<string, string>("Child", "[2.0.0, 3.0.0)");
            runtime.RuntimeUnresolvedDependencyEdges.Add(runtimeEdge);
            runtime.NonDevelopmentUnresolvedDependencyEdges.Add(runtimeEdge);
            test.NonDevelopmentUnresolvedDependencyEdges.Add(testEdge);
            test.AddDependencyEdge("DevOnlyChild", "1.0.0");
            var packages = new HashSet<DotnetDependency>();
            DotnetDependency.MergeInto(packages, reverse ? new[] { test, runtime } : new[] { runtime, test });
            var source = Assert.Single(packages);
            Assert.Equal(runtimeEdge, Assert.Single(source.RuntimeUnresolvedDependencyEdges));
            Assert.Equal(2, source.NonDevelopmentUnresolvedDependencyEdges.Count);
            Assert.Contains(runtimeEdge, source.NonDevelopmentUnresolvedDependencyEdges);
            Assert.Contains(testEdge, source.NonDevelopmentUnresolvedDependencyEdges);
        }
    }
}
