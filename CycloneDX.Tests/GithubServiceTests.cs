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
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CycloneDX.Services;
using RichardSzalay.MockHttp;
using Xunit;

namespace CycloneDX.Tests
{
    public class GithubServiceTests
    {
        [Fact]
        public async Task GitLicense_FromMasterBranch()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromRawMasterBranch()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/raw/master/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromMainBranch()
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
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/main/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_NonAmericanSpelling()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENCE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact(Skip = "Currently failing as GitHub license API only returns the current license")]
        public async Task GitLicense_FromVersionTag()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/v1.2.3/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromMarkdownExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.md").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromTextExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.txt").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromUpperCaseTextExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.TXT").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromBsdExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.bsd").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromUpperCaseBsdExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.BSD").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromMitExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.mit").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromUpperCaseMitExtensionLicense()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE.MIT").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromLicenseNameWithHyphenLicenseId()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE-MIT").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromLicenseFileWithPascalCaseFileName()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/License.txt").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromLicenseFileWithLowerCaseFileName()
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

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/license.txt").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromGithubUserContent()
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

            var license = await githubService.GetLicenseAsync("https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromRawGithub()
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

            var license = await githubService.GetLicenseAsync("https://raw.github.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromSshGithub()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet.git/license")
                .Respond(HttpStatusCode.NotFound);
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("git@github.com:CycloneDX/cyclonedx-dotnet.git").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
        }

        [Fact]
        public async Task GitLicense_FromGithubWithDotGit()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet.git/license")
                .Respond(HttpStatusCode.NotFound);
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet.git").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
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

            await githubService.GetLicenseAsync("https://raw.githubusercontent.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(true);

            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task GitLicense_DifferentHtmlUrl()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                },
                ""html_url"": ""https://licenceUrl.com""
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license?ref=master")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://raw.github.com/CycloneDX/cyclonedx-dotnet/master/LICENSE").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("https://licenceurl.com/", license.Url);
        }

        [Fact]
        public async Task GitLicense_NoRef()
        {
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""LicenseSpdxId"",
                    ""name"": ""Test License""
                },
                ""html_url"": ""https://licenceUrl.com""
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://raw.github.com/CycloneDX/cyclonedx-dotnet").ConfigureAwait(true);

            Assert.Equal("LicenseSpdxId", license.Id);
            Assert.Equal("https://licenceurl.com/", license.Url);
        }

        [Fact]
        public async Task GitLicense_ReplacesNoAssertionWithNull()
        {
            //See also https://github.com/CycloneDX/cyclonedx-dotnet/issues/525

            var mockResponseContent = @"{
                ""license"": {                    
                    ""name"": ""Other"",
                    ""spdx_id"": ""NOASSERTION""                  
                }, 
                ""html_url"": ""https://licenceUrl.com""
            }";
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            var license = await githubService.GetLicenseAsync("https://raw.github.com/CycloneDX/cyclonedx-dotnet").ConfigureAwait(true);

            Assert.Null(license.Id);
            Assert.Equal("https://licenceurl.com/", license.Url);
        }
        [Fact]
        public async Task GitLicense_Redirect301ToHttpUrl_ThrowsGitHubLicenseResolutionException()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond(_ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                    response.Headers.Location = new Uri("http://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license");
                    return response;
                });
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            await Assert.ThrowsAsync<GitHubLicenseResolutionException>(
                () => githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet")).ConfigureAwait(true);
        }

        [Fact]
        public async Task GitLicense_RepositoryIdWithQueryInjection_DoesNotCallInjectedUrl()
        {
            // If the repositoryId captured from a GitHub URL contains URL metacharacters
            // (e.g. '?'), they must be percent-encoded before being interpolated into the
            // GitHub API URL so they cannot inject extra query parameters.
            //
            // The GitHub regex captures repositoryId with [^\/]+\/[^\/]+ which allows '?'.
            // For a URL whose repo slug already contains a percent-encoded '?' (%3F), after
            // the fix the service must further encode '%' to '%25', producing '%253F' in the
            // API path — ensuring the literal character never acts as a query delimiter.
            //
            // We verify this by checking that the mock HTTP handler — which only matches the
            // safe, doubly-encoded URL — is satisfied (meaning the unsafe raw-? URL was not hit).
            var mockResponseContent = @"{
                ""license"": {
                    ""spdx_id"": ""MIT"",
                    ""name"": ""MIT License""
                }
            }";
            var mockHttp = new MockHttpMessageHandler();
            // The encoded URL: %3F in the repo slug is re-encoded to %253F by EscapeDataString
            mockHttp.When("https://api.github.com/repos/owner/repo%253Ffoo%253Dbar/license")
                .Respond("application/json", mockResponseContent);
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            // Input URL has a percent-encoded '?' in the repository slug
            var license = await githubService.GetLicenseAsync(
                "https://github.com/owner/repo%3Ffoo%3Dbar").ConfigureAwait(true);

            // The mock only matches the safely-encoded URL; if the raw '?' or wrong encoding
            // were used the mock would throw, causing the test to fail.
            Assert.Equal("MIT", license?.Id);
        }

        [Fact]
        public async Task GitLicense_Redirect301ToForeignDomain_ThrowsGitHubLicenseResolutionException()
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .When("https://api.github.com/repos/CycloneDX/cyclonedx-dotnet/license")
                .Respond(_ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                    response.Headers.Location = new Uri("https://attacker.example.com/steal-credentials");
                    return response;
                });
            var client = mockHttp.ToHttpClient();
            var githubService = new GithubService(client);

            await Assert.ThrowsAsync<GitHubLicenseResolutionException>(
                () => githubService.GetLicenseAsync("https://github.com/CycloneDX/cyclonedx-dotnet")).ConfigureAwait(true);
        }

    }
}
