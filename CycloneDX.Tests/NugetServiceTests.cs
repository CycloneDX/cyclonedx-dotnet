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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using RichardSzalay.MockHttp;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class NugetServiceTests
    {
        //TODO test baseUrl override
        
        [Fact]
        public async Task GetComponent_ReturnsName()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>PackageName</id>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("PackageName", component.Name);
        }

        [Fact]
        public async Task GetComponent_ReturnsVersion()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <version>1.2.3</version>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("1.2.3", component.Version);
        }

        [Fact]
        public async Task GetComponent_ReturnsPurl()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>PackageName</id>
                    <version>1.2.3</version>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("pkg:nuget/PackageName@1.2.3", component.Purl);
        }

        [Fact]
        public async Task GetComponent_ReturnsPublisher()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <authors>Authors</authors>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("Authors", component.Publisher);
        }

        [Fact]
        public async Task GetComponent_ReturnsCopyright()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <copyright>Copyright notice</copyright>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("Copyright notice", component.Copyright);
        }

        [Fact]
        public async Task GetComponent_ReturnsDescription()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <summary>Package summary</summary>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("Package summary", component.Description);
        }

        [Fact]
        public async Task GetComponent_WithoutSummaryReturns_NugetDescription()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <description>Package description</description>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("Package description", component.Description);
        }

        [Fact]
        public async Task GetComponent_WithoutDescription_ReturnsNugetTitle()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <title>Package title</title>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Equal("Package title", component.Description);
        }

        [Fact]
        public async Task GetComponent_ReturnsLicense()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <license type=""expression"">Apache-2.0</license>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Collection(component.Licenses,
                item => {
                    Assert.Equal("Apache-2.0", item.Id);
                    Assert.Equal("Apache-2.0", item.Name);
                });
        }

        [Fact]
        public async Task GetComponent_ReturnsLicenseUrl()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <licenseUrl>https://www.example.com/license</licenseUrl>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            
            Assert.Collection(component.Licenses,
                item => Assert.Equal("https://www.example.com/license", item.Url));
        }

        [Fact]
        public async Task GetComponent_ReturnsDependencies()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <dependencies>
                        <dependency id=""Dependency1"" version=""1.2.3""/>
                        <dependency id=""Dependency2"" version=""1.0.0""/>
                        <dependency id=""Dependency3"" version=""3.0.1""/>
                    </dependencies>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");
            var orderedDependencies = new List<Models.NugetPackage>(component.Dependencies);
            orderedDependencies.Sort();
            
            Assert.Collection(orderedDependencies,
                item => {
                    Assert.Equal("Dependency1", item.Name);
                    Assert.Equal("1.2.3", item.Version);
                },
                item => {
                    Assert.Equal("Dependency2", item.Name);
                    Assert.Equal("1.0.0", item.Version);
                },
                item => {
                    Assert.Equal("Dependency3", item.Name);
                    Assert.Equal("3.0.1", item.Version);
                });
        }

        [Fact]
        public async Task GetImaginaryComponent_ReturnsComponent()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/PackageName/1.2.3/PackageName.nuspec")
                .Respond(System.Net.HttpStatusCode.NotFound);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3");

            Assert.Equal("PackageName", component.Name);
            Assert.Equal("1.2.3", component.Version);
        }

        [Fact]
        public async Task GetImplicitVersionComponent_ReturnsNull()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponentAsync("PackageName", "");

            Assert.Null(component);
        }
    }
}
