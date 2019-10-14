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
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using RichardSzalay.MockHttp;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class NugetServiceTest
    {
        [Fact]
        public async Task GetComponentReturnsCoreComponentInformation()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>Package.Name</id>
                    <version>1.2.3</version>
                    <authors>Authors</authors>
                    <summary>Package summary</summary>
                    <copyright>Copyright notice</copyright>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/Package.Name/1.2.3/Package.Name.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponent("Package.Name", "1.2.3");
            
            Assert.Equal("Package.Name", component.Name);
            Assert.Equal("1.2.3", component.Version);
            Assert.Equal("pkg:nuget/Package.Name@1.2.3", component.Purl);
            Assert.Equal("Authors", component.Publisher);
            Assert.Equal("Copyright notice", component.Copyright);
            Assert.Equal("Package summary", component.Description);
        }

        [Fact]
        public async Task GetComponentWithoutSummaryReturnsNugetDescription()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <description>Package description</description>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/Microsoft.Extensions.Logging/3.0.0/Microsoft.Extensions.Logging.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponent("Microsoft.Extensions.Logging", "3.0.0");
            
            Assert.Equal("Package description", component.Description);
        }

        [Fact]
        public async Task GetComponentWithoutDescriptionReturnsNugetTitle()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <title>Package title</title>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/Microsoft.Extensions.Logging/3.0.0/Microsoft.Extensions.Logging.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponent("Microsoft.Extensions.Logging", "3.0.0");
            
            Assert.Equal("Package title", component.Description);
        }

        [Fact]
        public async Task GetComponentWithSingleExpressionLicenseReturnsLicense()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <license type=""expression"">Apache-2.0</license>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/Microsoft.Extensions.Logging/3.0.0/Microsoft.Extensions.Logging.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponent("Microsoft.Extensions.Logging", "3.0.0");
            
            Assert.Single(component.Licenses);
            Assert.Equal("Apache-2.0", component.Licenses.First().Id);
            Assert.Equal("Apache-2.0", component.Licenses.First().Name);
        }

        [Fact]
        public async Task GetComponentWithSingleLicenseUrlReturnsLicenseUrl()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <licenseUrl>https://www.example.com/license</licenseUrl>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/Microsoft.Extensions.Logging/3.0.0/Microsoft.Extensions.Logging.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(httpClient: client);

            var component = await nugetService.GetComponent("Microsoft.Extensions.Logging", "3.0.0");
            
            Assert.Single(component.Licenses);
            Assert.Equal("https://www.example.com/license", component.Licenses.First().Url);
        }
    }
}
