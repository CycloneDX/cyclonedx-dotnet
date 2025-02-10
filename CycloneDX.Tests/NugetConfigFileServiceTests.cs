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
using System.Linq;

namespace CycloneDX.Tests
{
    public class NugetConfigFileServiceTests
    {
        [Fact]
        public async Task GetDotnetDependencys_ReturnsDotnetDependency()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\nuget.config"), Helpers.GetNugetConfigFileWithSources(
                        new List<NugetInputModel> {
                            new NugetInputModel( "https://www.contoso.com" ) { nugetFeedName = "Contoso" }
                        })
                    },
                });
            var configFileService = new NugetConfigFileService(mockFileSystem);

            var sources = await configFileService.GetPackageSourcesAsync(XFS.Path(@"c:\Project\nuget.config")).ConfigureAwait(true);
            
            Assert.Collection(sources,
                item => {
                    Assert.Equal("https://www.contoso.com", item.nugetFeedUrl);
                });
            Assert.Collection(sources,
                item => {
                    Assert.Equal("Contoso", item.nugetFeedName);
                });
        }

        [Fact]
        public async Task GetDotnetDependencys_ReturnsMultipleDotnetDependencys()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\nuget.config"), Helpers.GetNugetConfigFileWithSources(
                        new List<NugetInputModel> {
                            new NugetInputModel( "https://www.contoso.com" ) { nugetFeedName = "Contoso" },
                            new NugetInputModel( "https://www.contoso2.com" ) { nugetFeedName = "Contoso2" },
                            new NugetInputModel( "https://www.contoso3.com" ) { nugetFeedName = "Contoso3" },
                        })
                    },
                });
            var configFileService = new NugetConfigFileService(mockFileSystem);

            var sources = await configFileService.GetPackageSourcesAsync(XFS.Path(@"c:\Project\nuget.config")).ConfigureAwait(true);
            var sortedPackages = new List<NugetInputModel>(sources);
            sortedPackages.OrderBy(nim => nim.nugetFeedName);

            Assert.Collection(sortedPackages,
                item => Assert.Equal("https://www.contoso.com", item.nugetFeedUrl),
                item => Assert.Equal("https://www.contoso2.com", item.nugetFeedUrl),
                item => Assert.Equal("https://www.contoso3.com", item.nugetFeedUrl));
            Assert.Collection(sortedPackages,
                item => Assert.Equal("Contoso", item.nugetFeedName),
                item => Assert.Equal("Contoso2", item.nugetFeedName),
                item => Assert.Equal("Contoso3", item.nugetFeedName));
        }

    }
}
