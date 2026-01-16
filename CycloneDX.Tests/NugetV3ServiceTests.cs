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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using NuGet.Common;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class NugetV3ServiceTests
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
            var nugetService = new NugetV3Service(null, mockFileSystem, cachePaths, mockGithubService.Object,
                new NullLogger(), false);

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

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("testpackage", component.Name);
        }

        public static IEnumerable<object[]> VersionNormalization
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { "2.5", "2.5" },
                    new object[] { "2.5.0.0", "2.5.0" },
                    new object[] { "2.5.0.0-beta.1", "2.5.0-beta.1" },
                    new object[] { "2.5.1.0", "2.5.1" },
                    new object[] { "2.5.1.1", "2.5.1.1" }
                };
            }
        }

        [Theory]
        [MemberData(nameof(VersionNormalization))]
        public async Task GetComponent_FromCachedNuspecFile_UsesNormalizedVersions(string rawVersion, string normalizedVersion)
        {
            var nuspecFileContents = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <version>{rawVersion}</version>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path($@"c:\nugetcache\testpackage\{normalizedVersion}\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", rawVersion, Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("testpackage", component.Name);
            Assert.Equal(rawVersion, component.Version);
        }

        [Theory]
        [MemberData(nameof(VersionNormalization))]
        public async Task GetComponent_FromCachedNugetFile_ReturnsComponentWithHashUsingNormalizedVersion(string rawVersion, string normalizedVersion)
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
                { XFS.Path($@"c:\nugetcache\testpackage\{normalizedVersion}\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path($@"c:\nugetcache\testpackage\{normalizedVersion}\testpackage.{normalizedVersion}.nupkg"), new MockFileData(nugetFileContent) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", $"{rawVersion}", Component.ComponentScope.Required).ConfigureAwait(true);

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

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(),
                true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Null(component.Hashes);
        }

        public static IEnumerable<object[]> VcsUrlNormalization
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { null, null },                    

                    // Blank
                    new object[] { "", null },
                    new object[] { "   ", null },

                    // Leading and trailing whitespace
                    new object[] { "  git@github.com:LordVeovis/xmlrpc.git", "https://git:@github.com/LordVeovis/xmlrpc.git" },
                    new object[] { "git@github.com:LordVeovis/xmlrpc.git  ", "https://git:@github.com/LordVeovis/xmlrpc.git" },
                    new object[] { "  git@github.com:LordVeovis/xmlrpc.git  ", "https://git:@github.com/LordVeovis/xmlrpc.git" },

                    // Relative
                    new object[] { "gitlab.dontcare.com:group/repo.git", "gitlab.dontcare.com:group/repo.git" },
                    new object[] { "git@gitlab.dontcare.com:group/repo.git", "https://git:@gitlab.dontcare.com/group/repo.git" },

                    // Absolute
                    new object[] { "gitlab.dontcare.com:/group/repo.git", "gitlab.dontcare.com:/group/repo.git" },
                    new object[] { "git@gitlab.dontcare.com:/group/repo.git", "https://git:@gitlab.dontcare.com/group/repo.git" },

                    // Colon in path
                    new object[] { "git@gitlab.dontcare.com:/group:with:colons/repo.git", "https://git:@gitlab.dontcare.com/group:with:colons/repo.git" },

                    // Invalid
                    new object[] { "  + ", null },
                    new object[] { "user@@gitlab.com:/rooted/Thinktecture.Logging.Configuration.git", null },

                    // Port number
                    new object[] { "https://github.com:443/CycloneDX/cyclonedx-dotnet.git", "https://github.com/CycloneDX/cyclonedx-dotnet.git" },
                    new object[] { "https://user:@github.com:443/CycloneDX/cyclonedx-dotnet.git", "https://user:@github.com/CycloneDX/cyclonedx-dotnet.git" },
                    new object[] { "https://user:password@github.com:443/CycloneDX/cyclonedx-dotnet.git", "https://user:password@github.com/CycloneDX/cyclonedx-dotnet.git" },

                    // Valid
                    new object[] { "https://github.com/CycloneDX/cyclonedx-dotnet.git", "https://github.com/CycloneDX/cyclonedx-dotnet.git" }
                };
            }
        }

        [Theory]
        [MemberData(nameof(VcsUrlNormalization))]
        public async Task GetComponent_FromCachedNuspecFile_UsesNormalizedVcsUrl(string rawVcsUrl, string normalizedVcsUrl)
        {
            var nuspecFileContents = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <repository type=""git"" url=""{rawVcsUrl}"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path($@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("testpackage", component.Name);
            Assert.Equal(normalizedVcsUrl, component.ExternalReferences?.FirstOrDefault()?.Url);
        }

        [Fact]
        public async Task GetComponentFromNugetOrgReturnsComponent()
        {
            var nugetService = new NugetV3Service(null,
                new MockFileSystem(),
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var packageName = "Newtonsoft.Json";
            var packageVersion = "13.0.1";
            var component = await nugetService
                .GetComponentAsync("Newtonsoft.Json", packageVersion, Component.ComponentScope.Required)
                .ConfigureAwait(true);

            Assert.Equal(packageName, component.Name);
            Assert.Equal(packageVersion, component.Version);
        }

        [Fact]
        public async Task GetComponentFromNugetOrgReturnsComponent_disableHashComputation_true()
        {
            var nugetService = new NugetV3Service(null,
                new MockFileSystem(),
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), true);

            var packageName = "Newtonsoft.Json";
            var packageVersion = "13.0.1";
            var component = await nugetService
                .GetComponentAsync("Newtonsoft.Json", packageVersion, Component.ComponentScope.Required)
                .ConfigureAwait(true);

            Assert.Equal(packageName, component.Name);
            Assert.Equal(packageVersion, component.Version);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <licenseUrl>https://licence.url</licenseUrl>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            mockGitHubService.Setup(x => x.GetLicenseAsync("https://licence.url")).Returns(Task.FromResult(new License { Id = "LicenseId" }));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            mockGitHubService.Verify(x => x.GetLicenseAsync(It.IsAny<string>()), Times.Once);
            Assert.Single(component.Licenses);
            Assert.Equal("LicenseId", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_FromRepository_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <repository url=""https://licence.url"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            mockGitHubService.Setup(x => x.GetLicenseAsync("https://licence.url")).Returns(Task.FromResult(new License { Id = "LicenseId" }));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            mockGitHubService.Verify(x => x.GetLicenseAsync(It.IsAny<string>()), Times.Once);
            Assert.Single(component.Licenses);
            Assert.Equal("LicenseId", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_FromRepositoryAndCommit_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <repository url=""https://licence.url"" commit=""123456"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            mockGitHubService.Setup(x => x.GetLicenseAsync("https://licence.url/blob/123456/licence")).Returns(Task.FromResult(new License { Id = "LicenseId" }));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            mockGitHubService.Verify(x => x.GetLicenseAsync(It.IsAny<string>()), Times.Once);
            Assert.Single(component.Licenses);
            Assert.Equal("LicenseId", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_FromProjectUrl_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <projectUrl>https://licence.url</projectUrl>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            mockGitHubService.Setup(x => x.GetLicenseAsync("https://licence.url")).Returns(Task.FromResult(new License { Id = "LicenseId" }));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            mockGitHubService.Verify(x => x.GetLicenseAsync(It.IsAny<string>()), Times.Once);
            Assert.Single(component.Licenses);
            Assert.Equal("LicenseId", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_FallsBackToLicenseFile()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">subdir/LICENSE.MD</license>
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\subdir\LICENSE.MD"), new MockFileData(licenseContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("testpackage License", component.Licenses.First().License.Name);
            Assert.Equal(Convert.ToBase64String(licenseContents), component.Licenses.First().License.Text.Content);
            Assert.Equal("base64", component.Licenses.First().License.Text.Encoding);
            Assert.Equal("text/markdown", component.Licenses.First().License.Text.ContentType);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_FromRepository_WhenLicenseInvalid_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <licenseUrl>https://not-licence.url</licenseUrl>
                    <repository url=""https://licence.url"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            mockGitHubService.Setup(x => x.GetLicenseAsync("https://licence.url")).Returns(Task.FromResult(new License { Id = "LicenseId" }));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            mockGitHubService.Verify(x => x.GetLicenseAsync(It.IsAny<string>()), Times.Exactly(2));
            Assert.Single(component.Licenses);
            Assert.Equal("LicenseId", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_SingleLicenseExpression_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""expression"">Apache-2.0</license>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("Apache-2.0", component.Licenses.First().License.Id);
        }

        [Fact]
        public async Task GetComponent_UnlicensedLicenseExpression_MapsToLicenseName()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""expression"">UNLICENSED</license>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("UNLICENSED", component.Licenses.First().License.Name);
            Assert.True(string.IsNullOrEmpty(component.Licenses.First().License.Id));
        }



        [Fact]
        public async Task GetComponent_MultiLicenseExpression_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""expression"">Apache-2.0 OR MPL-2.0</license>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
        });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal(2, component.Licenses.Count);
            Assert.Contains(component.Licenses, choice => choice.License.Id.Equals("Apache-2.0"));
            Assert.Contains(component.Licenses, choice => choice.License.Id.Equals("MPL-2.0"));
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_MaliciousCommitWithQueryString_DoesNotCallGitHub()
        {
            // A nuspec from an untrusted feed could carry a commit value containing URL
            // metacharacters designed to inject query parameters into a downstream HTTP call.
            // The service must reject such values rather than interpolating them verbatim.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <repository url=""https://github.com/owner/repo"" commit=""master?injected=evil"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            // Must never pass a URL containing the raw injected query string to GitHub
            mockGitHubService.Verify(
                x => x.GetLicenseAsync(It.Is<string>(url => url.Contains("injected=evil"))),
                Times.Never);
        }

        [Fact]
        public async Task GetComponent_GitHubLicenseLookup_MaliciousCommitWithFragment_DoesNotCallGitHub()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <repository url=""https://github.com/owner/repo"" commit=""abc#../../etc/passwd"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false);

            await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            // Must never pass a URL containing the injected fragment to GitHub
            mockGitHubService.Verify(
                x => x.GetLicenseAsync(It.Is<string>(url => url.Contains("etc/passwd"))),
                Times.Never);
        }

        [Fact]
        public async Task GetComponent_WhenGitHubServiceIsNullAndHasNoLicenseFile_UsesLicenseUrl()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <licenseUrl>https://not-licence.url</licenseUrl>
                    <repository url=""https://licence.url"" />
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("https://not-licence.url", component.Licenses.First().License.Url);
            Assert.Equal("Unknown - See URL", component.Licenses.First().License.Name);
        }

        [Fact]
        public async Task GetComponent_WhenGitHubServiceIsNull_UsesLicenseFile()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">subdir/LICENSE.MD</license>
                    <repository url=""https://licence.url"" />
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\subdir\LICENSE.MD"), new MockFileData(licenseContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("testpackage License", component.Licenses.First().License.Name);
            Assert.Equal(Convert.ToBase64String(licenseContents), component.Licenses.First().License.Text.Content);
            Assert.Equal("base64", component.Licenses.First().License.Text.Encoding);
            Assert.Equal("text/markdown", component.Licenses.First().License.Text.ContentType);
        }

        // -----------------------------------------------------------------------------------------
        // Target behavior tests — these expect the new --include-license-text parameter
        // (7th constructor argument). They will fail until the implementation is in place.
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task GetComponent_LicenseFile_WithoutFlag_NoGitHub_EmitsNoLicense()
        {
            // Phase 3 inactive (flag off): a <license type="file"> package with no <licenseUrl>
            // should produce no license entry at all — not a null-URL stub.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData(licenseContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Null(component.Licenses);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_NoGitHub_EmbedsText()
        {
            // Phase 3 active, no GitHub: <license type="file"> with --include-license-text
            // should embed the file content as base64.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData(licenseContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("testpackage License", component.Licenses.First().License.Name);
            Assert.Equal(Convert.ToBase64String(licenseContents), component.Licenses.First().License.Text.Content);
            Assert.Equal("base64", component.Licenses.First().License.Text.Encoding);
            Assert.Equal("text/plain", component.Licenses.First().License.Text.ContentType);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_GitHubMisses_EmbedsText()
        {
            // Phase 3 active, GitHub enabled but finds nothing: should fall through to the
            // license file and embed it.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.md</license>
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.md"), new MockFileData(licenseContents) },
            });

            var mockGitHubService = new Mock<IGithubService>();
            // GitHub returns null for all URLs (non-GitHub or missing)
            mockGitHubService.Setup(x => x.GetLicenseAsync(It.IsAny<string>())).Returns(Task.FromResult<License>(null));

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("testpackage License", component.Licenses.First().License.Name);
            Assert.Equal(Convert.ToBase64String(licenseContents), component.Licenses.First().License.Text.Content);
            Assert.Equal("base64", component.Licenses.First().License.Text.Encoding);
            Assert.Equal("text/markdown", component.Licenses.First().License.Text.ContentType);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_FileNotInCache_FallsBackToLicenseUrl()
        {
            // Phase 3 active but file missing from cache: should fall through to phase 4
            // and use <licenseUrl> if present.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                    <licenseUrl>https://example.com/license</licenseUrl>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                // LICENSE.txt deliberately absent from cache
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("Unknown - See URL", component.Licenses.First().License.Name);
            Assert.Equal("https://example.com/license", component.Licenses.First().License.Url);
            Assert.Null(component.Licenses.First().License.Text);
        }

        [Fact]
        public async Task GetComponent_NoLicenseInfo_EmitsNoLicense()
        {
            // Phase 4 fix: a package with no license metadata at all should produce no
            // license entry — not a null-URL stub.
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

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Null(component.Licenses);
        }

        [Fact]
        public async Task GetComponent_LicenseUrlOnly_EmitsUrlFallback()
        {
            // Phase 4: <licenseUrl> present, no file metadata → should still emit the URL entry.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <licenseUrl>https://example.com/license</licenseUrl>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal("Unknown - See URL", component.Licenses.First().License.Name);
            Assert.Equal("https://example.com/license", component.Licenses.First().License.Url);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_TxtExtension_UsesTextPlainContentType()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData("The license") },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("text/plain", component.Licenses.First().License.Text.ContentType);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_NoExtension_UsesTextPlainContentType()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE</license>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE"), new MockFileData("The license") },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("text/plain", component.Licenses.First().License.Text.ContentType);
        }

        [Fact]
        public async Task GetComponent_LicenseFile_WithFlag_UnknownExtension_UsesOctetStreamContentType()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.rtf</license>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.rtf"), new MockFileData("The license") },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Equal("application/octet-stream", component.Licenses.First().License.Text.ContentType);
        }

        // -----------------------------------------------------------------------------------------
        // aka.ms/deprecateLicenseUrl — NuGet auto-inserts this URL for <license type="file"> packs.
        // It is a dead redirect, not an actual license URL, so it must never appear in the BOM.
        // -----------------------------------------------------------------------------------------

        [Fact]
        public async Task GetComponent_FileLicense_AkaMsDeprecatedUrl_WithoutFlag_EmitsNoLicense()
        {
            // When a package has <license type="file"> NuGet auto-inserts
            // <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>.
            // Without --include-license-text, Phase 3 is inactive and Phase 4 must NOT fall back
            // to this known-useless redirect URL — the component should have no license entry.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                    <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData("The license") },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Null(component.Licenses);
        }

        [Fact]
        public async Task GetComponent_FileLicense_AkaMsDeprecatedUrl_WithFlag_EmbedsFile()
        {
            // When --include-license-text is on, Phase 3 should embed the file and Phase 4
            // (aka.ms URL) must never be reached. This confirms Phase 3 takes priority.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                    <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>
                </metadata>
                </package>";

            byte[] licenseContents = Encoding.UTF8.GetBytes("The license");

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData(licenseContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                null,
                new NullLogger(), false, includeLicenseText: true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Single(component.Licenses);
            Assert.Equal(Convert.ToBase64String(licenseContents), component.Licenses.First().License.Text.Content);
            Assert.DoesNotContain("aka.ms", component.Licenses.First().License.Url ?? "");
        }

        [Fact]
        public async Task GetComponent_FileLicense_AkaMsDeprecatedUrl_WithGitHub_SkipsApiCallAndEmitsNoLicense()
        {
            // When --enable-github-licenses is on and the only licenseUrl is the aka.ms stub,
            // the tool must NOT pass that URL to the GitHub API (it is not a GitHub URL and
            // the call would be wasted). No license node should appear in the BOM.
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                    <license type=""file"">LICENSE.txt</license>
                    <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>
                </metadata>
                </package>";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\LICENSE.txt"), new MockFileData("The license") },
            });

            var mockGitHubService = new Mock<IGithubService>();

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                mockGitHubService.Object,
                new NullLogger(), false, includeLicenseText: false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(true);

            Assert.Null(component.Licenses);
            mockGitHubService.Verify(x => x.GetLicenseAsync("https://aka.ms/deprecateLicenseUrl"), Times.Never);
        }
    }
}
