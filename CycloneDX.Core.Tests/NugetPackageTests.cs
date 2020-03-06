// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

using Xunit;
using CycloneDX.Models;

namespace CycloneDX.Tests
{
    public class NugetPackageTests
    {
        [Fact]
        public void SameNugetPackageVersions_AreEqual()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var nugetPackage2 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.3",
            };

            Assert.True(nugetPackage1.Equals((object)nugetPackage2));
        }

        [Fact]
        public void SameNugetPackages_WithDifferentVersions_AreNotEqual()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var nugetPackage2 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.False(nugetPackage1.Equals((object)nugetPackage2));
        }

        [Fact]
        public void DifferentNugetPackages_WithSameVersions_AreNotEqual()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var nugetPackage2 = new NugetPackage
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.False(nugetPackage1.Equals((object)nugetPackage2));
        }

        [Fact]
        public void NullNugetPackage_IsNotEqual()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.3",
            };

            // cast null as string to flex Equals(object obj) and Equals(NugetPackage other)
            Assert.False(nugetPackage1.Equals((string)null));
        }

        [Fact]
        public void NugetPackages_AreSortedByName()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var nugetPackage2 = new NugetPackage
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.Equal(-1, nugetPackage1.CompareTo(nugetPackage2));
        }

        [Fact]
        public void TheSameNugetPackages_AreSortedByVersion()
        {
            var nugetPackage1 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var nugetPackage2 = new NugetPackage
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.Equal(-1, nugetPackage1.CompareTo(nugetPackage2));
        }

        [Fact]
        public void NullNugetPackages_AreSortedFirst()
        {
            var nugetPackage = new NugetPackage
            {
                Name = "Package1",
                Version = "1.2.3",
            };

            // cast null as string to flex CompareTo(object obj) and CompareTo(NugetPackage other)
            Assert.Equal(1, nugetPackage.CompareTo((string)null));
        }
    }
}
