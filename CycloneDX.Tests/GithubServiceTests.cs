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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);
            
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/raw/master/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromMainBranch()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=main")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/main/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromRepositoryCommit()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=1234567890")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client, true);

            var parameters = new GetLicenseParameters
            {
                RepositoryUrl = "https://github.com/CycloneDX/cyclonedx-dotnet.git",
                CommitRef = "1234567890"
            };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromRepositoryCommitAndNotEnabled()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=1234567890")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/1234567890/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Null(license);
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/v1.2.3/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);
            
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.md" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);
            
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.txt" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);
            
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.TXT" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.bsd" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.BSD" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.mit" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.MIT" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE-MIT" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromLicenseFileWithPascalCaseFileName()
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/License.txt" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromLicenseFileWithLowerCaseFileName()
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/license.txt" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);
            
            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("Test License", license.Name);
        }

        [Fact]
        public async Task GitLicence_FromRawGithub()
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
            var githubService = new GithubService(client, false);

            var parameters = new GetLicenseParameters { LicenseUrl = "https://raw.github.com/CycloneDX/cyclonedx-dotnet/master/LICENSE" };
            var license = await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

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
            var githubService = new GithubService(client, false, "Aladdin", "open sesame");

            var parameters = new GetLicenseParameters { LicenseUrl = "https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE" };
            await githubService.GetLicenseAsync(parameters).ConfigureAwait(false);

            mockHttp.VerifyNoOutstandingExpectation();
        }
    }
}
