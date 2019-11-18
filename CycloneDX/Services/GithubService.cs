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

using CycloneDX.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using License = CycloneDX.Models.License;

namespace CycloneDX.Services
{

    public interface IGithubService
    {
        Task<License> GetLicenseAsync(string licenseUrl);
    }

    class GithubService : IGithubService
    {

        private string _baseUrl;
        private HttpClient _httpClient;
        private Regex _repositoryIdRegex = new Regex(@"https?\:\/\/[^\/]+\/(?<repositoryId>[^\/]+\/[^\/]+)\/.+$");

        public GithubService(HttpClient httpClient, string baseUrl = null)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl ?? "https://api.github.com/";
        }

        public async Task<License> GetLicenseAsync(string licenseUrl)
        {
            // Regex may let other URLs pass, a plain filter on URLs containing "github" may be enough
            if (licenseUrl == null || !licenseUrl.Contains("github")) return null;

            // Detect correct repository id starting from URL
            var match = _repositoryIdRegex.Match(licenseUrl);

            // License is not on GitHub, we need to abort
            if (!match.Success) return null;
            var repositoryId = match.Groups["repositoryId"];

            Console.WriteLine($"Retrieving GitHub license for repositoy {repositoryId}");

            // Generate request URL and message
            var githubLicenseUrl = $"{_baseUrl}repos/{repositoryId}/license";
            var githubLicenseRequestMessage = new HttpRequestMessage(HttpMethod.Get, githubLicenseUrl);
            // Add needed headers
            githubLicenseRequestMessage.Headers.UserAgent.ParseAdd("CycloneDX/1.0");
            githubLicenseRequestMessage.Headers.Accept.ParseAdd("application/json");
            // Send HTTP request
            var githubResponse = await _httpClient.SendAsync(githubLicenseRequestMessage);

            if (githubResponse.IsSuccessStatusCode)
            {
                // License found, extract data
                var githubLicense = JsonConvert.DeserializeObject<GithubLicenseRoot>(await githubResponse.Content.ReadAsStringAsync());
                return new License
                {
                    Id = githubLicense.License.SpdxId,
                    Name = githubLicense.License.Name,
                    Url = licenseUrl
                };
            }
            else
            {
                // License not found or any other error with GitHub APIs.
                Console.WriteLine($"GitHub API failed with status code {githubResponse.StatusCode} and message {githubResponse.ReasonPhrase}.");
                return null;
            }
        }
    }
}
