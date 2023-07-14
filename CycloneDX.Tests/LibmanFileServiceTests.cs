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
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class LibmanFileServiceTests
    {
        [Fact]
        public async Task GetLibmanPackages_ReturnsLibmanPackage()
        {
            // Arrange
            var content = @"{
              ""version"": ""1.0"",
              ""libraries"": [
                    {
                        ""provider"": ""cdnjs"",
                        ""library"": ""package@1.0.2""
                    }
                ]
            }";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\project\libman.json"), new MockFileData(content) },
            });

            var libmanFileService = new LibmanFileService(mockFileSystem);

            // Act
            var packages = await libmanFileService.GetLibmanPackagesAsync(XFS.Path(@"c:\project\libman.json")).ConfigureAwait(false);

            // Assert
            Assert.Collection(packages,
                item =>
                {
                    Assert.Equal("package", item.Name);
                    Assert.Equal("1.0.2", item.Version);
                    Assert.IsType<LibmanPackage>(item);
                    Assert.Equal(LibmanProvider.cdnjs, (item as LibmanPackage).Provider);
                });
        }

        [Fact]
        public async Task GetLibmanPackages_ReturnsDefaultProvider()
        {
            // Arrange
            var content = @"{
              ""version"": ""1.0"",
              ""defaultProvider"": ""unpkg"",
              ""libraries"": [
                    {
                        ""library"": ""package@1.0.2""
                    }
                ]
            }";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\project\libman.json"), new MockFileData(content) },
            });

            var libmanFileService = new LibmanFileService(mockFileSystem);

            // Act
            var packages = await libmanFileService.GetLibmanPackagesAsync(XFS.Path(@"c:\project\libman.json")).ConfigureAwait(false);

            // Assert
            Assert.Collection(packages,
                item =>
                {
                    Assert.IsType<LibmanPackage>(item);
                    Assert.Equal(LibmanProvider.unpkg, (item as LibmanPackage).Provider);
                });
        }
    }
}
