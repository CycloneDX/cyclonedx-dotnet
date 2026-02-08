using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDX.Models;
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
namespace CycloneDX
{
    internal static class ExcludeFilterHelper
    {

        /// <summary>
        /// Excludes specific packages from the provided set of dependencies based on the given filter.
        /// </summary>
        /// <param name="packages">The set of dependencies to filter.</param>
        /// <param name="excludeFilter">
        /// A comma-separated string of package identifiers in the format 'name@version' or 'name' to exclude.
        /// When only the name is provided, all versions of that package will be excluded.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if any package identifier in the filter is empty or invalid.
        /// </exception>
        internal static void ExcludePackages(HashSet<DotnetDependency> packages, string excludeFilter)
        {
            var packagesToExclude = excludeFilter.Split(',');
            foreach (var packageKey in packagesToExclude)
            {
                var trimmedKey = packageKey.Trim();
                if (string.IsNullOrWhiteSpace(trimmedKey))
                {
                    throw new ArgumentException("Package identifier cannot be empty.",
                        nameof(excludeFilter));
                }

                var packageKeyParts = trimmedKey.Split('@');
                var packageName = packageKeyParts[0];
                
                if (packageKeyParts.Length == 1)
                {
                    // Exclude all versions of the package
                    packages.RemoveWhere(p => p.Name == packageName);
                }
                else if (packageKeyParts.Length == 2)
                {
                    // Exclude specific version of the package
                    var packageToExclude = new DotnetDependency { Name = packageName, Version = packageKeyParts[1] };
                    packages.Remove(packageToExclude);
                }
                else
                {
                    throw new ArgumentException("Package identifier must be in format 'name' or 'name@version'.",
                        nameof(excludeFilter));
                }
            }
        }

        /// <summary>
        /// Finds all dependencies in the provided set that are part of the dependency graph.
        /// The graph traversal starts with all packages marked as direct references.
        /// </summary>
        /// <param name="packages">The set of dependencies to analyze.</param>
        /// <returns>A set of reachable dependencies.</returns>
        private static HashSet<DotnetDependency> FindReachableDependencies(HashSet<DotnetDependency> packages)
        {
            var reachableDependencies = new HashSet<DotnetDependency>();
            var queue = new Queue<DotnetDependency>(packages.Where(p => p.IsDirectReference));

            // BFS to collect all reachable dependencies in the graph
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                reachableDependencies.Add(current);

                foreach (var dependency in current.Dependencies)
                {
                    var key = new DotnetDependency { Name = dependency.Key, Version = dependency.Value };
                    if (packages.TryGetValue(key, out var actualDependency) && !reachableDependencies.Contains(actualDependency))
                    {
                        queue.Enqueue(actualDependency);
                    }
                }
            }

            return reachableDependencies;
        }

        /// <summary>
        /// Removes orphaned packages from the provided set of dependencies.
        /// Orphaned packages are those that are not directly referenced or reachable from direct references.
        /// </summary>
        /// <param name="packages">The set of dependencies to filter.</param>
        internal static void RemoveOrphanedPackages(HashSet<DotnetDependency> packages)
        {
            var reachablePackages = FindReachableDependencies(packages);
            var orphanedPackages = packages.Except(reachablePackages).ToList();

            // Log the removed orphaned packages
            if (orphanedPackages.Count != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("The following orphaned packages have been removed:");
                foreach (var orphan in orphanedPackages)
                {
                    Console.WriteLine($"  - {orphan.Name}@{orphan.Version}");
                }
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("No orphaned packages were found.");
            }

            // Remove orphaned packages
            packages.IntersectWith(reachablePackages);
        }
    }
}
