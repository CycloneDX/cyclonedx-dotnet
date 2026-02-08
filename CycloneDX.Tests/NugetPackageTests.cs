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

using Xunit;
using CycloneDX.Models;

namespace CycloneDX.Tests
{
    public class DotnetDependencyTests
    {
        [Fact]
        public void SameDotnetDependencyVersions_AreEqual()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var DotnetDependency2 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.3",
            };

            Assert.True(DotnetDependency1.Equals((object)DotnetDependency2));
        }

        [Fact]
        public void SameDotnetDependencys_WithDifferentVersions_AreNotEqual()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var DotnetDependency2 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.False(DotnetDependency1.Equals((object)DotnetDependency2));
        }

        [Fact]
        public void DifferentDotnetDependencys_WithSameVersions_AreNotEqual()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var DotnetDependency2 = new DotnetDependency
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.False(DotnetDependency1.Equals((object)DotnetDependency2));
        }

        [Fact]
        public void NullDotnetDependency_IsNotEqual()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.3",
            };

            // cast null as string to flex Equals(object obj) and Equals(DotnetDependency other)
            Assert.False(DotnetDependency1.Equals((string)null));
        }

        [Fact]
        public void DotnetDependencys_AreSortedByName()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var DotnetDependency2 = new DotnetDependency
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.Equal(-1, DotnetDependency1.CompareTo(DotnetDependency2));
        }

        [Fact]
        public void TheSameDotnetDependencys_AreSortedByVersion()
        {
            var DotnetDependency1 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var DotnetDependency2 = new DotnetDependency
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.Equal(-1, DotnetDependency1.CompareTo(DotnetDependency2));
        }

        [Fact]
        public void NullDotnetDependencys_AreSortedFirst()
        {
            var DotnetDependency = new DotnetDependency
            {
                Name = "Package1",
                Version = "1.2.3",
            };

            // cast null as string to flex CompareTo(object obj) and CompareTo(DotnetDependency other)
            Assert.Equal(1, DotnetDependency.CompareTo((string)null));
        }
    }
}
