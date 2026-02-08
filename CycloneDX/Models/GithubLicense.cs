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
using System.Text.Json.Serialization;

namespace CycloneDX.Models
{

    public partial class GithubLicenseRoot
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        [JsonPropertyName("html_url")]
        public Uri HtmlUrl { get; set; }

        [JsonPropertyName("git_url")]
        public Uri GitUrl { get; set; }

        [JsonPropertyName("download_url")]
        public Uri DownloadUrl { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("encoding")]
        public string Encoding { get; set; }

        [JsonPropertyName("_links")]
        public Links Links { get; set; }

        [JsonPropertyName("license")]
        public GithubLicense License { get; set; }
    }

    public partial class GithubLicense
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("spdx_id")]
        public string SpdxId { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }
    }

    public partial class Links
    {
        [JsonPropertyName("self")]
        public Uri Self { get; set; }

        [JsonPropertyName("git")]
        public Uri Git { get; set; }

        [JsonPropertyName("html")]
        public Uri Html { get; set; }
    }
}
