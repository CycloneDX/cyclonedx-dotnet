using System;
using System.Text.Json.Serialization;

namespace CycloneDX.Core.Models
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
