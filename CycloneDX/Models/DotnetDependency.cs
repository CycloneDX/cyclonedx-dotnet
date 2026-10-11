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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CycloneDX.Models
{
    public enum DependencyType
    {
        Package,
        Project
    }

    [SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes")]
    public class DotnetDependency : IComparable
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsDirectReference { get; set; }
        public bool IsDevDependency { get; set; }
        // Known local occurrences are classified before project/framework graphs are merged.
        // Null keeps the legacy/no-graph fallback available to the caller.
        public bool? IsRuntimeReachable { get; set; }
        // Test-project exclusion affects runtime scope, not development-dependency filtering.
        public bool? IsNonDevelopmentReachable { get; set; }
        public DependencyType DependencyType { get; set; }
        public Component.ComponentScope? Scope { get; set; }
        public string Path { get; set; }


        public Dictionary<string, string> Dependencies { get; set; } //key: name ~ value: version

        public HashSet<KeyValuePair<string, string>> AdditionalDependencyEdges { get; } = new();

        // Only edges from sources reached in their original target graph belong here.
        // These retain provenance for resolving missing local dependencies after merging.
        public HashSet<KeyValuePair<string, string>> RuntimeUnresolvedDependencyEdges { get; } = new();
        public HashSet<KeyValuePair<string, string>> NonDevelopmentUnresolvedDependencyEdges { get; } = new();

        public IEnumerable<KeyValuePair<string, string>> GetDependencyEdges()
        {
            return (Dependencies ?? new Dictionary<string, string>()).Concat(AdditionalDependencyEdges).Distinct();
        }

        public void AddDependencyEdge(string name, string version)
        {
            Dependencies ??= new Dictionary<string, string>();
            if (!Dependencies.TryGetValue(name, out var existingVersion))
                Dependencies.Add(name, version);
            else if (existingVersion != version)
                AdditionalDependencyEdges.Add(new KeyValuePair<string, string>(name, version));
        }

        /// <summary>Merge occurrences without losing classification, roots, or graph edges.</summary>
        public static void MergeInto(HashSet<DotnetDependency> packages, IEnumerable<DotnetDependency> incoming)
        {
            foreach (var dependency in incoming)
            {
                if (!packages.TryGetValue(dependency, out var existing))
                {
                    packages.Add(dependency);
                    continue;
                }

                existing.IsDirectReference |= dependency.IsDirectReference;
                existing.IsDevDependency &= dependency.IsDevDependency;
                if (existing.IsRuntimeReachable.HasValue && dependency.IsRuntimeReachable.HasValue)
                    existing.IsRuntimeReachable |= dependency.IsRuntimeReachable;
                else
                    existing.IsRuntimeReachable = null;
                if (existing.IsNonDevelopmentReachable.HasValue && dependency.IsNonDevelopmentReachable.HasValue)
                    existing.IsNonDevelopmentReachable |= dependency.IsNonDevelopmentReachable;
                else
                    existing.IsNonDevelopmentReachable = null;
                if (dependency.Scope == Component.ComponentScope.Required || !existing.Scope.HasValue)
                    existing.Scope = dependency.Scope;

                existing.RuntimeUnresolvedDependencyEdges.UnionWith(dependency.RuntimeUnresolvedDependencyEdges);
                existing.NonDevelopmentUnresolvedDependencyEdges.UnionWith(dependency.NonDevelopmentUnresolvedDependencyEdges);

                existing.Dependencies ??= new Dictionary<string, string>();
                foreach (var edge in dependency.GetDependencyEdges().ToList())
                {
                    existing.AddDependencyEdge(edge.Key, edge.Value);
                }
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DotnetDependency;
            return this.Equals(other);
        }

        public bool Equals(DotnetDependency other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return this.Name == other.Name && this.Version == other.Version;
            }
        }

        public override int GetHashCode()
        {
            return Utils.GeneratePackageUrl(Name, Version).GetHashCode();
        }

        public int CompareTo(DotnetDependency other)
        {
            if (other == null)
            {
                return 1;
            }
            else
            {
                var nameComparison = string.Compare(this.Name, other.Name, StringComparison.Ordinal);
                return nameComparison == 0
                    ? string.Compare(this.Version, other.Version, StringComparison.Ordinal)
                    : nameComparison;
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as DotnetDependency;
            return CompareTo(other);
        }

        public override string ToString()
        {
            return $"{Name}@{Version}"; 
        }
    }
}
