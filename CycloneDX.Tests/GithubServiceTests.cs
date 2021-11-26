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
using System.Threading.Tasks;
using Xunit;
using RichardSzalay.MockHttp;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class GithubServiceTests
    {
        [Fact]
        public async Task GitLicence_FromMasterBranch()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE").ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromRawMasterBranch()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/raw/master/LICENSE").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact(Skip="Currently failing as GitHub license API only returns the current license")]
        public async Task GitLicence_FromVersionTag()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=v1.2.3")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/v1.2.3/LICENSE").ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromMarkdownExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.md").ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromTextExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.txt").ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromUpperCaseTextExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.TXT").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromBsdExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.bsd").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromUpperCaseBsdExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.BSD").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromMitExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.mit").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromUpperCaseMitExtensionLicense()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.MIT").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromLicenseNameWithHyphenLicenseId()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE-MIT").ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromGithubUserContent()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GetLicense_AddsAuthorizationHeader()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .Expect("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .WithHeaders("Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client, "Aladdin", "open sesame");

            await githubService.GetLicenseAsync("https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(false);

            mockHttp.VerifyNoOutstandingExpectation();
        }
    }
}
