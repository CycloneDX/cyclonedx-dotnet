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
using System.Collections.Generic;
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

    public class GithubService : IGithubService
    {

        private string _baseUrl = "https://api.github.com/";
        private HttpClient _httpClient;
        private List<Regex> _githubRepositoryRegexes = new List<Regex>
        {
            new Regex(@"^https?\:\/\/github\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/blob\/(?<refSpec>[^\/]+)\/LICENSE(.md)?$"),
            new Regex(@"^https?\:\/\/raw\.githubusercontent\.com\/(?<repositoryId>[^\/]+\/[^\/]+)\/(?<refSpec>[^\/]+)\/LICENSE(.md)?$"),
        };

        public GithubService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Tries to get a license from GitHub.
        /// </summary>
        /// <param name="licenseUrl">URL for the license file. Supporting both github.com and raw.githubusercontent.com URLs.</param>
        /// <returns></returns>
        public async Task<License> GetLicenseAsync(string licenseUrl)
        {
            if (licenseUrl == null) return null;

            // Detect correct repository id starting from URL
            Match match = null;

            foreach(var regex in _githubRepositoryRegexes)
            {
                match = regex.Match(licenseUrl);
                if (match.Success) break;
            }

            // License is not on GitHub, we need to abort
            if (!match.Success) return null;
            var repositoryId = match.Groups["repositoryId"];
            var refSpec = match.Groups["refSpec"];

            Console.WriteLine($"Retrieving GitHub license for repository {repositoryId} and ref {refSpec}");

            // Try getting license for the specified version
            var githubLicense = await GetGithubLicenseAsync($"{_baseUrl}repos/{repositoryId}/license?ref={refSpec}");

            if (githubLicense == null) {
                Console.WriteLine($"No license found on GitHub for repository {repositoryId} using ref {refSpec}");

                return null;
            }

            // If we have a license we can map it to its return format
            return new License
            {
                Id = githubLicense.License.SpdxId,
                Name = githubLicense.License.Name,
                Url = licenseUrl
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
            var githubResponse = await _httpClient.SendAsync(githubLicenseRequestMessage);
            if (githubResponse.IsSuccessStatusCode)
            {
                // License found, extract data
                return JsonConvert.DeserializeObject<GithubLicenseRoot>(await githubResponse.Content.ReadAsStringAsync());
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
