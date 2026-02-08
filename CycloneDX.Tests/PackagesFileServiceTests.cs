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
using System.Threading.Tasks;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class PackagesFileServiceTests
    {
        [Fact]
        public async Task GetDotnetDependencys_ReturnsDotnetDependency()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\packages.config"), Helpers.GetPackagesFileWithPackageReference("Package", "1.2.3") },
                });
            var packagesFileService = new PackagesFileService(mockFileSystem);

            var packages = await packagesFileService.GetDotnetDependencysAsync(XFS.Path(@"c:\Project\packages.config")).ConfigureAwait(true);
            
            Assert.Collection(packages,
                item => {
                    Assert.Equal("Package", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                });
        }

        [Fact]
        public async Task GetDotnetDependencys_ReturnsMultipleDotnetDependencys()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\packages.config"), Helpers.GetPackagesFileWithPackageReferences(
                        new List<DotnetDependency> {
                            new DotnetDependency { Name = "Package1", Version = "1.2.3"},
                            new DotnetDependency { Name = "Package2", Version = "1.2.3"},
                            new DotnetDependency { Name = "Package3", Version = "1.2.3"},
                        })
                    },
                });
            var packagesFileService = new PackagesFileService(mockFileSystem);

            var packages = await packagesFileService.GetDotnetDependencysAsync(XFS.Path(@"c:\Project\packages.config")).ConfigureAwait(true);
            var sortedPackages = new List<DotnetDependency>(packages);
            sortedPackages.Sort();

            Assert.Collection(sortedPackages,
                item => Assert.Equal("Package1", item.Name),
                item => Assert.Equal("Package2", item.Name),
                item => Assert.Equal("Package3", item.Name));
        }

    }
}
