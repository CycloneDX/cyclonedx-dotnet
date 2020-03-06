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
    public class ComponentTests
    {
        [Fact]
        public void SameComponentVersions_AreEqual()
        {
            var component1 = new Component
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var component2 = new Component
            {
                Name = "Package",
                Version = "1.2.3",
            };

            Assert.True(component1.Equals((object)component2));
        }

        [Fact]
        public void SameComponents_WithDifferentVersions_AreNotEqual()
        {
            var component1 = new Component
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var component2 = new Component
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.False(component1.Equals((object)component2));
        }

        [Fact]
        public void DifferentComponents_WithSameVersions_AreNotEqual()
        {
            var component1 = new Component
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var component2 = new Component
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.False(component1.Equals((object)component2));
        }

        [Fact]
        public void NullComponent_IsNotEqual()
        {
            var component = new Component
            {
                Name = "Package",
                Version = "1.2.3",
            };

            // cast null as string to flex Equals(object obj) and Equals(NugetPackage other)
            Assert.False(component.Equals((string)null));
        }

        [Fact]
        public void Components_AreSortedByName()
        {
            var component1 = new Component
            {
                Name = "Package1",
                Version = "1.2.3",
            };
            var component2 = new Component
            {
                Name = "Package2",
                Version = "1.2.3",
            };

            Assert.Equal(-1, component1.CompareTo(component2));
        }

        [Fact]
        public void TheSameComponents_AreSortedByVersion()
        {
            var component1 = new Component
            {
                Name = "Package",
                Version = "1.2.3",
            };
            var component2 = new Component
            {
                Name = "Package",
                Version = "1.2.4",
            };

            Assert.Equal(-1, component1.CompareTo(component2));
        }

        [Fact]
        public void NullComponents_AreSortedFirst()
        {
            var component = new Component
            {
                Name = "Package1",
                Version = "1.2.3",
            };

            // cast null as string to flex CompareTo(object obj) and CompareTo(NugetPackage other)
            Assert.Equal(1, component.CompareTo((string)null));
        }
    }
}
