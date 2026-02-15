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

using System.Collections.Generic;
using Xunit;
using CycloneDX.Models;

namespace CycloneDX.Tests
{
    /// <summary>
    /// Tests for issue #1025: Dependencies are dropped on letter case mismatches between referenced and defined NuGets
    /// NuGet package IDs are case-insensitive according to the spec, but the tool was treating them as case-sensitive.
    /// </summary>
    public class CaseInsensitivePackageNameTests
    {
        [Fact]
        public void DotnetDependency_Equals_ShouldBeCaseInsensitive()
        {
            // Arrange - Create two dependencies with same package but different casing
            // This simulates "Newtonsoft.Json" referenced as "NewtonSoft.Json"
            var dep1 = new DotnetDependency 
            { 
                Name = "Newtonsoft.Json", 
                Version = "13.0.1" 
            };
            
            var dep2 = new DotnetDependency 
            { 
                Name = "NewtonSoft.Json",  // Different casing
                Version = "13.0.1" 
            };

            // Act & Assert - These should be considered equal according to NuGet spec
            Assert.True(dep1.Equals(dep2), 
                "Dependencies with same name but different casing should be equal (NuGet IDs are case-insensitive)");
        }

        [Fact]
        public void DotnetDependency_GetHashCode_ShouldBeCaseInsensitive()
        {
            // Arrange
            var dep1 = new DotnetDependency 
            { 
                Name = "Newtonsoft.Json", 
                Version = "13.0.1" 
            };
            
            var dep2 = new DotnetDependency 
            { 
                Name = "NewtonSoft.Json", 
                Version = "13.0.1" 
            };

            // Act & Assert - Hash codes should be the same for case-insensitive equality
            Assert.Equal(dep1.GetHashCode(), dep2.GetHashCode());
        }

        [Fact]
        public void HashSet_ShouldNotContainDuplicatesWithDifferentCasing()
        {
            // Arrange - This simulates how dependencies are collected
            var dependencies = new HashSet<DotnetDependency>();
            
            var dep1 = new DotnetDependency 
            { 
                Name = "Newtonsoft.Json", 
                Version = "13.0.1" 
            };
            
            var dep2 = new DotnetDependency 
            { 
                Name = "NewtonSoft.Json",  // Different casing
                Version = "13.0.1" 
            };

            // Act
            dependencies.Add(dep1);
            dependencies.Add(dep2);

            // Assert - Should only have 1 item, not 2
            Assert.Single(dependencies);
        }

        [Fact]
        public void DotnetDependency_Equals_DifferentVersions_ShouldNotBeEqual()
        {
            // Arrange
            var dep1 = new DotnetDependency 
            { 
                Name = "Newtonsoft.Json", 
                Version = "13.0.1" 
            };
            
            var dep2 = new DotnetDependency 
            { 
                Name = "Newtonsoft.Json", 
                Version = "13.0.2" 
            };

            // Act & Assert
            Assert.False(dep1.Equals(dep2));
        }

        [Fact]
        public void DotnetDependency_CompareTo_ShouldBeCaseInsensitive()
        {
            // Arrange
            var dep1 = new DotnetDependency 
            { 
                Name = "newtonsoft.json", 
                Version = "13.0.1" 
            };
            
            var dep2 = new DotnetDependency 
            { 
                Name = "NEWTONSOFT.JSON", 
                Version = "13.0.1" 
            };

            // Act & Assert - Should be considered equal (CompareTo returns 0)
            Assert.Equal(0, dep1.CompareTo(dep2));
        }
    }
}
