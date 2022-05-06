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
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;
using Moq;
using RichardSzalay.MockHttp;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class NugetServiceTests
    {
        [Fact]
        public void GetCachedNuspecFilename_ReturnsFullPath()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache1\dummypackage\1.2.3\dummypackage.nuspec"), "" },
                { XFS.Path(@"c:\nugetcache2\testpackage\1.2.3\testpackage.nuspec"), "" },
            });
            var cachePaths = new List<string>
            {
                XFS.Path(@"c:\nugetcache1"),
                XFS.Path(@"c:\nugetcache2"),
            };
            var mockGithubService = new Mock<IGithubService>();
            var nugetService = new NugetService(
                mockFileSystem,
                cachePaths,
                mockGithubService.Object,
                new HttpClient());

            var nuspecFilename = nugetService.GetCachedNuspecFilename("TestPackage", "1.2.3");

            Assert.Equal(XFS.Path(@"c:\nugetcache2\testpackage\1.2.3\testpackage.nuspec"), nuspecFilename);
        }

        [Fact]
        public async Task GetComponent_FromCachedNuspecFile_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });
            var nugetService = new NugetService(
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new HttpClient());

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal("testpackage", component.Name);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetHashFile_ReturnsComponentWithHash()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";
            byte[] sampleHash = new byte[]{1,2,3,4,5,6,78,125,200};

            var nugetHashFileContents = Convert.ToBase64String(sampleHash);
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg.sha512"), new MockFileData(nugetHashFileContents) },
            });
            var nugetService = new NugetService(
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new HttpClient());

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal(Hash.HashAlgorithm.SHA_512, component.Hashes[0].Alg);
            Assert.Equal(BitConverter.ToString(sampleHash).Replace("-", string.Empty), component.Hashes[0].Content);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetFile_ReturnsComponentWithHash()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";

            var nugetFileContent = "FooBarBaz";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg"), new MockFileData(nugetFileContent) },
            });
            var nugetService = new NugetService(
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new HttpClient());

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            byte[] hashBytes;
            using (SHA512 sha = SHA512.Create())
            {
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(nugetFileContent));
            }

            Assert.Equal(Hash.HashAlgorithm.SHA_512, component.Hashes[0].Alg);
            Assert.Equal(BitConverter.ToString(hashBytes).Replace("-", string.Empty), component.Hashes[0].Content);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetFile_DoNotReturnsHash_WhenDisabled()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";

            var nugetFileContent = "FooBarBaz";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg"), new MockFileData(nugetFileContent) },
            });
            var nugetService = new NugetService(
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new HttpClient(),
                disableHashComputation: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Null(component.Hashes);
        }

        [Fact]
        public async Task GetComponent_FromNugetOrg_ReturnsComponent()
        {
            var mockResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <name>testpackage</name>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/testpackage/1.0.0/testpackage.nuspec")
                .Respond("application/xml", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                new Mock<IGithubService>().Object,
                client);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal("testpackage", component.Name);
        }

        [Fact]
        public async Task GetComponent_FromNugetOrg_ReturnsComponentWithHash()
        {
            var mockNuspecResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <name>testpackage</name>
                </metadata>
                </package>";
            var mockNugetResponseContent = @"123464dfdgfgr87rgDSGSGGDgdg4s58g6f7g5d;';243'2;446";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/testpackage/1.0.0/testpackage.nuspec")
                .Respond("application/xml", mockNuspecResponseContent);
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/testpackage/1.0.0/testpackage.1.0.0.nupkg")
                .Respond("application/xml", mockNugetResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                new Mock<IGithubService>().Object,
                client);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);


            byte[] hashBytes;
            using (SHA512 sha = SHA512.Create())
            {
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(mockNugetResponseContent));
            }

            Assert.Equal("testpackage", component.Name);
            Assert.Equal(Hash.HashAlgorithm.SHA_512, component.Hashes[0].Alg);
            Assert.Equal(BitConverter.ToString(hashBytes).Replace("-", string.Empty), component.Hashes[0].Content);
        }

        [Fact]
        public async Task GetComponent_FromNugetOrg_DoNotReturnsHash_WhenDisabled()
        {
            var mockNuspecResponseContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <name>testpackage</name>
                </metadata>
                </package>";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/testpackage/1.0.0/testpackage.nuspec")
                .Respond("application/xml", mockNuspecResponseContent);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                new Mock<IGithubService>().Object,
                client,
                disableHashComputation: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);


            Assert.Equal("testpackage", component.Name);
            Assert.Null(component.Hashes);
        }

        [Fact]
        public async Task GetComponent_FromNugetOrgWhichDoesntExist_ReturnsComponent()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/testpackage/1.0.0/testpackage.nuspec")
                .Respond(HttpStatusCode.NotFound);
            var client = mockHttp.ToHttpClient();
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                new Mock<IGithubService>().Object,
                client);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal("testpackage", component.Name);
        }

        [Fact]
        public async Task GetComponent_WithGithubLicense_ReturnsGithubLicense()
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
            var mockGithubService = new Mock<IGithubService>();
            mockGithubService
                .Setup(service => service.GetLicenseAsync(It.IsAny<string>()))
                .ReturnsAsync(new License
                {
                    Id = "TestLicenseId",
                    Name = "Test License",
                    Url = "https://www.example.com/LICENSE"
                });
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                mockGithubService.Object,
                client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Collection(component.Licenses,
                item => {
                    Assert.Equal("TestLicenseId", item.License.Id);
                    Assert.Equal("Test License", item.License.Name);
                    Assert.Equal("https://www.example.com/LICENSE", item.License.Url);
                });
        }

        [Fact]
        public async Task GetComponent_WithGithubLicenseResolutionDisabled_DoesntResolveGithubLicense()
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
            var nugetService = new NugetService(
                new MockFileSystem(),
                new List<string>(),
                null,
                client);

            var component = await nugetService.GetComponentAsync("PackageName", "1.2.3", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Collection(component.Licenses,
                item => {
                    Assert.Null(item.License.Id);
                    Assert.Null(item.License.Name);
                    Assert.Equal("https://www.example.com/license", item.License.Url);
                });
        }
    }
}
