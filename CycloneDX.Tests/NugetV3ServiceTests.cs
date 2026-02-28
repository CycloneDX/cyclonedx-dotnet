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
        public async Task GetComponent_WhenGitHubServiceIsNull_UsesLicenseUrl()
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
    }
}
