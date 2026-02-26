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
using System.Collections.Concurrent;
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

    public interface IGithubService
    {
        Task<License> GetLicenseAsync(string licenseUrl);
    }

    public class GithubService : IGithubService
    {
        private readonly ConcurrentDictionary<string, Task<License>> licenseCache = new ConcurrentDictionary<string, Task<License>>();
        private string _baseUrl = "https://api.github.com/";
        private readonly HttpClient _httpClient;

        private readonly List<Regex> _githubRepositoryRegexes = new List<Regex>
        {
            new Regex(@"^https?\:\/\/github\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/((blob)|(raw))\/(?<refSpec>[^\/]+)\/[l][i][c][e][n][cs][e]((\.|-)((md)|([t][x][t])|([m][i][t])|([b][s][d])))?$", RegexOptions.IgnoreCase),
            new Regex(@"^https?\:\/\/raw\.github(usercontent)?\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/(?<refSpec>[^\/]+)\/[l][i][c][e][n][cs][e]((\.|-)((md)|([t][x][t])|([m][i][t])|([b][s][d])))?$", RegexOptions.IgnoreCase),
            new Regex(@"^https?\:\/\/github\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/?$", RegexOptions.IgnoreCase),
            new Regex(@"^https?\:\/\/raw\.github(usercontent)?\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/?$", RegexOptions.IgnoreCase),
            new Regex(@"^git\@github\.com:(?<repositoryId>[^\/]+\/[^\/]+)\/?$", RegexOptions.IgnoreCase)
        };

        public GithubService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public GithubService(HttpClient httpClient, string username, string token)
        {
            Contract.Requires(httpClient != null);
            _httpClient = httpClient;

            // implemented as per RFC 7617 https://tools.ietf.org/html/rfc7617.html
            var userToken = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", username, token);
            var userTokenBytes = System.Text.Encoding.UTF8.GetBytes(userToken);
            var userTokenBase64 = Convert.ToBase64String(userTokenBytes);

            var authorizationHeader = new AuthenticationHeaderValue(
                "Basic",
                userTokenBase64);

            _httpClient.DefaultRequestHeaders.Authorization = authorizationHeader;
        }

        public GithubService(HttpClient httpClient, string bearerToken)
        {
            Contract.Requires(httpClient != null);
            _httpClient = httpClient;

            var authorizationHeader = new AuthenticationHeaderValue(
                "Bearer",
                bearerToken);

            _httpClient.DefaultRequestHeaders.Authorization = authorizationHeader;
        }

        /// <summary>
        /// Tries to get a license from GitHub.
        /// </summary>
        /// <param name="licenseUrl">URL for the license file. Supporting both github.com and raw.githubusercontent.com URLs.</param>
        /// <returns></returns>
        public async Task<License> GetLicenseAsync(string licenseUrl)
        {
            if (licenseUrl == null) return null;

            return await licenseCache.GetOrAdd(licenseUrl, GetLicenseForCacheAsync);
        }

        private async Task<License> GetLicenseForCacheAsync(string licenseUrl)
        {
            // Detect correct repository id starting from URL
            Match match = null;

            foreach (var regex in _githubRepositoryRegexes)
            {
                match = regex.Match(licenseUrl);
                if (match.Success) { break; }
            }

            // License is not on GitHub, we need to abort
            if (!match.Success) return null;
            var repositoryId = match.Groups["repositoryId"].Value;
            string refSpec = null;

            if (match.Groups["refSpec"].Success)
            {
                refSpec = match.Groups["refSpec"].Value;
            }

            // GitHub API doesn't necessarily return the correct license for any ref other than master
            // support ticket has been raised, in the meantime will ignore non-master refs
            if (refSpec != null && refSpec != "master" && refSpec != "main") { return null; }

            // Try getting license for the specified version
            GithubLicenseRoot githubLicense = null;
            try
            {
                githubLicense = await GetGithubLicenseAsync(repositoryId, refSpec).ConfigureAwait(false);
            }
            catch (HttpRequestException exc)
            {
                Console.Error.WriteLine($"GitHub license resolution failed: {exc.Message}");
                Console.Error.WriteLine("For offline environments use --disable-github-licenses to disable GitHub license resolution.");
                throw new GitHubLicenseResolutionException("GitHub license resolution failed.", exc);
            }

            if (githubLicense == null)
            {
                Console.WriteLine($"No license found on GitHub for repository {repositoryId} using ref {refSpec}");

                return null;
            }

            // If we have a license we can map it to its return format
            var license = new License();

            license.Id = githubLicense.License.SpdxId != "NOASSERTION" ? githubLicense.License.SpdxId : null;
            license.Name = license.Id == null ? githubLicense.License.Name : null;
            license.Url = githubLicense.HtmlUrl?.ToString() ?? licenseUrl;
            
            return license;
        }

        /// <summary>
        /// Executes a request to GitHub's API.
        /// </summary>
        /// <param name="githubLicenseUrl"></param>
        /// <returns></returns>
        private async Task<GithubLicenseRoot> GetGithubLicenseAsync(string repositoryId, string refSpec)
        {
            string githubLicenseUrl;
            if (string.IsNullOrWhiteSpace(refSpec))
            {
                githubLicenseUrl = $"{_baseUrl}repos/{repositoryId}/license";
                Console.WriteLine($"Retrieving GitHub license for repository {repositoryId} - URL: {githubLicenseUrl}");
            }
            else
            {
                githubLicenseUrl = $"{_baseUrl}repos/{repositoryId}/license?ref={refSpec}";
                Console.WriteLine($"Retrieving GitHub license for repository {repositoryId} and ref {refSpec} - URL: {githubLicenseUrl}");
            }

            var githubLicenseRequestMessage = ConfigureGithubRequestMessage(githubLicenseUrl);

            // Send HTTP request and handle its response
            var githubResponse = await _httpClient.SendAsync(githubLicenseRequestMessage).ConfigureAwait(false);
            
            if (githubResponse.StatusCode == System.Net.HttpStatusCode.MovedPermanently && githubResponse.Headers.Location != null)
            {
                var redirectUri = githubResponse.Headers.Location;
                ValidateRedirectIsToGithub(redirectUri);
                githubLicenseRequestMessage = ConfigureGithubRequestMessage(redirectUri.ToString());
                githubResponse = await _httpClient.SendAsync(githubLicenseRequestMessage).ConfigureAwait(false);
            }
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
            else if (githubResponse.StatusCode == System.Net.HttpStatusCode.NotFound && repositoryId.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine("GitHub API failed with status code NotFound - will try again without '.git' on the repository name");
                return await GetGithubLicenseAsync(repositoryId.Substring(0, repositoryId.Length - 4), refSpec);
            }
            else
            {
                // License not found or any other error with GitHub APIs.
                Console.WriteLine($"GitHub API failed with status code {githubResponse.StatusCode} and message {githubResponse.ReasonPhrase}.");
                return null;
            }
        }
        private HttpRequestMessage ConfigureGithubRequestMessage(String githubUrl) {
            var githubRequestMessage = new HttpRequestMessage(HttpMethod.Get, githubUrl);

            // Add needed headers
            githubRequestMessage.Headers.UserAgent.ParseAdd("CycloneDX/1.0");
            githubRequestMessage.Headers.Accept.ParseAdd("application/json");

            return githubRequestMessage;
	    }

        /// <summary>
        /// Validates that a redirect URI still points to GitHub: it must use HTTPS and point to a
        /// GitHub-owned host so that the Authorization header is never forwarded elsewhere.
        /// </summary>
        private static void ValidateRedirectIsToGithub(Uri redirectUri)
        {
            if (!string.Equals(redirectUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubLicenseResolutionException(
                    $"Redirect to non-HTTPS URL rejected: {redirectUri}");
            }

            var isGitHubHost =
                redirectUri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase) ||
                redirectUri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
                redirectUri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

            if (!isGitHubHost)
            {
                throw new GitHubLicenseResolutionException(
                    $"Redirect to non-GitHub URL rejected: {redirectUri}");
            }
        }
    }
}
