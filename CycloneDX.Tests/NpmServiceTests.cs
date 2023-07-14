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

using System.Net;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;
using RichardSzalay.MockHttp;
using Xunit;

namespace CycloneDX.Tests
{
    public class NpmServiceTests
    {
        [Fact]
        public async Task GetComponentAsync_ReturnsComponent()
        {
            // Arrange
            var mockResponseContent = @"{
                ""name"": ""testpackage"",
                ""description"": ""Test Description"",
                ""homepage"": ""https://www.google.com"",
                ""license"": ""MIT"",
                ""author"": {
                    ""name"": ""Test Author"",
                    ""url"": ""https://www.homepage.com""
                }
            }";

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When($"{NpmService.BaseUrl}/testpackage/").Respond("application/json", mockResponseContent);

            var npmService = new NpmService(mockHttp.ToHttpClient());
            var package = new LibmanPackage(LibmanProvider.cdnjs)
            {
                Name = "testpackage",
                Version = "1.0.2"
            };

            // Act
            var component = await npmService.GetComponentAsync(package).ConfigureAwait(false);

            // Assert
            var packageUrl = Utils.GeneratePackageUrl(PackageType.Libman, "testpackage", "1.0.2");

            Assert.Equal(Component.Classification.Library, component.Type);
            Assert.Equal("testpackage", component.Name);
            Assert.Equal("1.0.2", component.Version);
            Assert.Equal("Test Description", component.Description);
            Assert.Equal("https://www.google.com", component.ExternalReferences[0].Url);
            Assert.Equal("MIT", component.Licenses[0].License.Id);
            Assert.Equal("Test Author", component.Author);
            Assert.Equal(packageUrl, component.BomRef);
            Assert.Equal(packageUrl, component.Purl);
        }

        [Fact]
        public async Task GetComponentAsync_NotFoundReturnsNull()
        {
            // Arrange           
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When($"{NpmService.BaseUrl}/testpackage/").Respond(HttpStatusCode.NotFound);

            var service = new NpmService(mockHttp.ToHttpClient());
            var package = new LibmanPackage(LibmanProvider.cdnjs)
            {
                Name = "testpackage",
                Version = "1.0.2"
            };

            // Act
            var component = await service.GetComponentAsync(package).ConfigureAwait(false);

            // Assert
            Assert.Null(component);
        }
    }
}
