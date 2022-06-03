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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class InvalidGitHubApiCredentialsException : Exception
    {
        public InvalidGitHubApiCredentialsException() : base() {}

        public InvalidGitHubApiCredentialsException(string message) : base(message) {}

        public InvalidGitHubApiCredentialsException(string message, Exception innerException) : base(message, innerException) {}
    }

    public class GitHubApiRateLimitExceededException : Exception
    {
        public GitHubApiRateLimitExceededException() : base() {}

        public GitHubApiRateLimitExceededException(string message) : base(message) {}

        public GitHubApiRateLimitExceededException(string message, Exception innerException) : base(message, innerException) {}
    }

    public class GitHubLicenseResolutionException : Exception
    {
        public GitHubLicenseResolutionException() : base() {}

        public GitHubLicenseResolutionException(string message) : base(message) {}

        public GitHubLicenseResolutionException(string message, Exception innerException) : base(message, innerException) {}
    }

    public class GetLicenseParameters
    {
        /// <summary>
        /// URL for the license file. Supporting both github.com and raw.githubusercontent.com URLs.
        /// </summary>
        public string LicenseUrl { get; set; }

        /// <summary>
        /// The URL of the Git repository. Only GitHub repositories are supported.
        /// </summary>
        public string RepositoryUrl { get; set; }

        /// <summary>
        /// The Git commit ID linked with the Nuget package Git repository.
        /// </summary>
        public string CommitRef { get; set; }
    }

    public interface IGithubService
    {
        Task<License> GetLicenseAsync(GetLicenseParameters parameters);
    }

    public class GithubService : IGithubService
    {
        private const string BaseUrl = "https://api.github.com/";

        private static readonly Regex GitHubRepositoryRegex = new Regex(@"^https?\:\/\/github\.com\/(?<repositoryId>[^\/]+\/[^\/]+)(?:\.git)", RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex[] GitHubLicenseUrlRegexes =
        {
            new Regex(@"^https?\:\/\/github\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/((blob)|(raw))\/(?<refSpec>[^\/]+)\/[Ll][Ii][Cc][Ee][Nn][Ss][Ee]((\.|-)((md)|([Tt][Xx][Tt])|([Mm][Ii][Tt])|([Bb][Ss][Dd])))?$", RegexOptions.Singleline | RegexOptions.Compiled),
            new Regex(@"^https?\:\/\/raw\.github(usercontent)?\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/(?<refSpec>[^\/]+)\/[Ll][Ii][Cc][Ee][Nn][Ss][Ee]((\.|-)((md)|([Tt][Xx][Tt])|([Mm][Ii][Tt])|([Bb][Ss][Dd])))?$", RegexOptions.Singleline | RegexOptions.Compiled),
        };

        private readonly HttpClient _httpClient;
        private readonly bool _allowNonDefaultBranch;

        public GithubService(HttpClient httpClient, bool allowNonDefaultBranch)
        {
            _httpClient = httpClient;
            _allowNonDefaultBranch = allowNonDefaultBranch;
        }

        public GithubService(HttpClient httpClient, bool allowNonDefaultBranch, string username, string token)
            : this(httpClient, allowNonDefaultBranch, GetBasicAuthorizationHeader(username, token))
        {
        }

        public GithubService(HttpClient httpClient, bool allowNonDefaultBranch, string bearerToken)
            : this(httpClient, allowNonDefaultBranch, new AuthenticationHeaderValue("Bearer", bearerToken))
        {
        }

        private GithubService(HttpClient httpClient, bool allowNonDefaultBranch, AuthenticationHeaderValue authorizationHeader)
        {
            Contract.Requires(httpClient != null);
            _httpClient = httpClient;
            _allowNonDefaultBranch = allowNonDefaultBranch;
            _httpClient.DefaultRequestHeaders.Authorization = authorizationHeader;
        }

        /// <summary>
        /// Tries to get a license from GitHub.
        /// </summary>
        /// <returns></returns>
        public async Task<License> GetLicenseAsync(GetLicenseParameters parameters)
        {
            if (parameters == null) { return null; }

            // Try to get license using the LicenseUrl listed in the .nuspec file
            if (!string.IsNullOrWhiteSpace(parameters.LicenseUrl))
            {
                // Detect correct repository id starting from URL
                foreach (var regex in GitHubLicenseUrlRegexes)
                {
                    var match = regex.Match(parameters.LicenseUrl);
                    if (match.Success)
                    {
                        var repositoryId = match.Groups["repositoryId"];
                        var refSpec = match.Groups["refSpec"];
                        return await GetLicenseFromRepositoryAndRefAsync(repositoryId.Value, refSpec.Value)
                            .ConfigureAwait(false);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(parameters.RepositoryUrl) &&
                !string.IsNullOrWhiteSpace(parameters.CommitRef))
            {
                var match = GitHubRepositoryRegex.Match(parameters.RepositoryUrl);
                if (match.Success)
                {
                    var repositoryId = match.Groups["repositoryId"];
                    return await GetLicenseFromRepositoryAndRefAsync(repositoryId.Value, parameters.CommitRef)
                        .ConfigureAwait(false);
                }
            }

            return null;
        }

        private async Task<License> GetLicenseFromRepositoryAndRefAsync(string repositoryId, string refSpec)
        {
            // GitHub API doesn't necessarily return the correct license for any ref other than master
            // support ticket has been raised, in the meantime will ignore non-master refs
            if (!this._allowNonDefaultBranch && refSpec != "master" && refSpec != "main")
            {
                return null;
            }

            Console.WriteLine($"Retrieving GitHub license for repository {repositoryId} and ref {refSpec}");

            // Try getting license for the specified version
            GithubLicenseRoot githubLicense;
            try
            {
                githubLicense = await GetGithubLicenseAsync($"{BaseUrl}repos/{repositoryId}/license?ref={refSpec}")
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exc)
            {
                Console.Error.WriteLine($"GitHub license resolution failed: {exc.Message}");
                Console.Error.WriteLine(
                    "For offline environments use --disable-github-licenses to disable GitHub license resolution.");
                throw new GitHubLicenseResolutionException("GitHub license resolution failed.", exc);
            }

            if (githubLicense == null)
            {
                Console.WriteLine($"No license found on GitHub for repository {repositoryId} using ref {refSpec}");
                return null;
            }

            // If we have a license we can map it to its return format
            return new License
            {
                Id = githubLicense.License.SpdxId,
                Name = githubLicense.License.Name,
                Url = (githubLicense.HtmlUrl ?? githubLicense.DownloadUrl)?.ToString(),
            };
        }

        /// <summary>
        /// Executes a request to GitHub's API.
        /// </summary>
        /// <param name="githubLicenseUrl"></param>
        /// <returns></returns>
        private async Task<GithubLicenseRoot> GetGithubLicenseAsync(string githubLicenseUrl)
        {
            var githubLicenseRequestMessage = new HttpRequestMessage(HttpMethod.Get, githubLicenseUrl);

            // Add needed headers
            githubLicenseRequestMessage.Headers.UserAgent.ParseAdd("CycloneDX/1.0");
            githubLicenseRequestMessage.Headers.Accept.ParseAdd("application/json");

            // Send HTTP request and handle its response
            var githubResponse = await _httpClient.SendAsync(githubLicenseRequestMessage).ConfigureAwait(false);
            if (githubResponse.IsSuccessStatusCode)
            {
                // License found, extract data
                return JsonSerializer.Deserialize<GithubLicenseRoot>(await githubResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            else if (githubResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.Error.WriteLine("Invalid GitHub API credentials.");
                throw new InvalidGitHubApiCredentialsException("Invalid GitHub API credentials http status code 401");
            }
            else if (githubResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.Error.WriteLine("GitHub API rate limit exceeded.");
                throw new GitHubApiRateLimitExceededException("GitHub API rate limit exceeded http status code 403");
            }
            else
            {
                // License not found or any other error with GitHub APIs.
                Console.WriteLine($"GitHub API failed with status code {githubResponse.StatusCode} and message {githubResponse.ReasonPhrase}.");
                return null;
            }
        }

        private static AuthenticationHeaderValue GetBasicAuthorizationHeader(string username, string token)
        {
            // implemented as per RFC 7617 https://tools.ietf.org/html/rfc7617.html
            var userToken = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", username, token);
            var userTokenBytes = System.Text.Encoding.UTF8.GetBytes(userToken);
            var userTokenBase64 = Convert.ToBase64String(userTokenBytes);
            var authorizationHeader = new AuthenticationHeaderValue("Basic", userTokenBase64);
            return authorizationHeader;
        }
    }
}
