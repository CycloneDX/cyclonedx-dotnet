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

        private static DotnetDependency Package(string name, string version, bool direct = false,
            params (string Name, string Version)[] dependencies)
        {
            return new DotnetDependency
            {
                Name = name,
                Version = version,
                IsDirectReference = direct,
                DependencyType = DependencyType.Package,
                Dependencies = dependencies.ToDictionary(d => d.Name, d => d.Version)
            };
        }

        /// <summary>
        /// Root -> Middle -> Leaf, plus Stray which nothing references and which is not a direct reference.
        /// </summary>
        private static HashSet<DotnetDependency> Graph()
        {
            return new HashSet<DotnetDependency>
            {
                Package("Root", "1.0.0", direct: true, dependencies: ("Middle", "1.0.0")),
                Package("Middle", "1.0.0", dependencies: ("Leaf", "1.0.0")),
                Package("Leaf", "1.0.0"),
                Package("Stray", "1.0.0")
            };
        }

        [Fact]
        public void FindUnreachableDependencies_ReturnsOnlyPackagesWithNoPathFromADirectReference()
        {
            var unreachable = ExcludeFilterHelper.FindUnreachableDependencies(Graph());

            Assert.Single(unreachable);
            Assert.Equal("Stray", unreachable.Single().Name);
        }

        [Fact]
        public void RemoveOrphanedPackages_RemovesPackagesTheFilterOrphaned()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Middle");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.DoesNotContain(packages, p => p.Name == "Middle");
            Assert.DoesNotContain(packages, p => p.Name == "Leaf");
            Assert.Contains(packages, p => p.Name == "Root");
        }

        [Fact]
        public void RemoveOrphanedPackages_KeepsPackagesThatWereAlreadyUnreachable()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Middle");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.Contains(packages, p => p.Name == "Stray");
        }

        /// <summary>
        /// An already unreachable package named by the filter is still removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_StillRemovesAnExplicitlyExcludedOrphan()
        {
            var packages = Graph();
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.ExcludePackages(packages, "Stray");
            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.DoesNotContain(packages, p => p.Name == "Stray");
            Assert.Equal(3, packages.Count);
        }

        /// <summary>
        /// Without a seed of direct references the whole graph is unreachable. Nothing may be removed,
        /// rather than everything.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_WithoutAnyDirectReference_RemovesNothing()
        {
            var packages = new HashSet<DotnetDependency>
            {
                Package("Root", "1.0.0", dependencies: ("Middle", "1.0.0")),
                Package("Middle", "1.0.0")
            };
            var preExistingOrphans = ExcludeFilterHelper.FindUnreachableDependencies(packages);

            ExcludeFilterHelper.RemoveOrphanedPackages(packages, preExistingOrphans);

            Assert.Equal(2, packages.Count);
        }

        /// <summary>
        /// The parameterless behaviour is unchanged: every unreachable package is removed.
        /// </summary>
        [Fact]
        public void RemoveOrphanedPackages_WithoutPreExistingOrphans_RemovesEveryUnreachablePackage()
        {
            var packages = Graph();

            ExcludeFilterHelper.RemoveOrphanedPackages(packages);

            Assert.DoesNotContain(packages, p => p.Name == "Stray");
            Assert.Equal(3, packages.Count);
        }
    }
}
