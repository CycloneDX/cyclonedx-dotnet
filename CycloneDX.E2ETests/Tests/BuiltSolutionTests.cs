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
using System.Collections.Generic;
using System.IO;
using CycloneDX.E2ETests.Builders;
using CycloneDX.E2ETests.Infrastructure;
using Xunit;

namespace CycloneDX.E2ETests.Tests
{
    public class BuiltSolutionTests
    {
        [Theory]
        [InlineData("Consumer")]
        [InlineData("Unused/../Consumer")]
        public void ReadProjectAssets_ReadsWithinSolution(string projectName)
        {
            using var parent = new TempDirectory();
            using var solution = new BuiltSolution(new TempDirectory(parent), "unused.sln", new List<string>());
            var assetsDirectory = BoundedPath(parent.Path, Path.Combine(solution.RootDir, "Consumer", "obj"));
            Directory.CreateDirectory(assetsDirectory);
            File.WriteAllText(BoundedPath(parent.Path, Path.Combine(assetsDirectory, "project.assets.json")), "{\"targets\":{}}");

            Assert.Equal("{\"targets\":{}}", solution.ReadProjectAssets(projectName));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadProjectAssets_RejectsEscapeToSiblingWithMatchingPrefix(bool absolute)
        {
            using var parent = new TempDirectory();
            using var solution = new BuiltSolution(new TempDirectory(parent), "unused.sln", new List<string>());
            // A plain prefix check would incorrectly allow this sibling directory.
            var sibling = solution.RootDir + "-outside";
            try
            {
                Directory.CreateDirectory(BoundedPath(parent.Path, Path.Combine(sibling, "obj")));
                File.WriteAllText(BoundedPath(parent.Path, Path.Combine(sibling, "obj", "project.assets.json")), "outside");
                var projectName = absolute ? sibling : Path.GetRelativePath(solution.RootDir, sibling);

                Assert.Throws<ArgumentException>(() => solution.ReadProjectAssets(projectName));
            }
            finally
            {
                Directory.Delete(BoundedPath(parent.Path, sibling), recursive: true);
            }
        }

        // The escape fixture is outside the solution but must stay inside our owned
        // parent, including during recursive cleanup. This is lexical containment;
        // it does not defend against symlinks or a hostile temp-root owner.
        private static string BoundedPath(string parentDirectory, string candidate)
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentDirectory)) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(candidate);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (path.Length < root.Length ||
                string.Compare(path, 0, root, 0, root.Length, comparison) != 0)
                throw new ArgumentException("Fixture path must be inside the test-owned parent directory.", nameof(candidate));

            return path;
        }
    }
}
