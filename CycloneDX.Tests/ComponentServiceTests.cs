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
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;

namespace CycloneDX.Tests
{
    public class ComponentServiceTests
    {
        [Fact]
        public async Task RecursivelyGetComponents_ReturnsComponents()
        {
            var mockNugetService = new Mock<INugetService>();
            mockNugetService
                .SetupSequence(service => service.GetComponentAsync(It.IsAny<NugetPackage>()))
                .ReturnsAsync(new Component { Name = "Package1", Version = "1.0.0" })
                .ReturnsAsync(new Component { Name = "Package2", Version = "1.0.0" })
                .ReturnsAsync(new Component { Name = "Package3", Version = "1.0.0" });
            var nugetService = mockNugetService.Object;
            var componentService = new ComponentService(nugetService);
            var nugetPackages = new List<NugetPackage>
            {
                new NugetPackage { Name = "Package1", Version = "1.0.0" },
                new NugetPackage { Name = "Package2", Version = "1.0.0" },
                new NugetPackage { Name = "Package3", Version = "1.0.0" },
            };

            var components = await componentService.RecursivelyGetComponentsAsync(nugetPackages).ConfigureAwait(false);
            var sortedComponents = components.OrderBy(c => c.Name).ToList();

            Assert.Collection(sortedComponents,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name)
            );
        }
    }
}
