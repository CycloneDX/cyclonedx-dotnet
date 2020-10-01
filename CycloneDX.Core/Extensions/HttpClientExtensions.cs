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

using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CycloneDX.Extensions
{
    public static class HttpClientExtensions
    {
        /*
         * Simple extension method to retrieve an xml stream from a URL.
         */
        public static async Task<Stream> GetXmlStreamAsync(this HttpClient httpClient, string url)
        {
            Contract.Requires(httpClient != null);
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            
            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.NotFound:
                // JFrog's Artifactory tends to return 405 errors instead of 404
                // errors when something can't be found.
                case System.Net.HttpStatusCode.MethodNotAllowed:
                    return null;
            }

            response.EnsureSuccessStatusCode();
            var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            return contentStream;
        }
    }
}