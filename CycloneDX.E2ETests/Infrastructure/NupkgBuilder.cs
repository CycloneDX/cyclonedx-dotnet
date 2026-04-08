// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.IO;
using System.IO.Compression;
using System.Text;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Builds minimal valid .nupkg files in memory.
    /// </summary>
    internal static class NupkgBuilder
    {
        public static byte[] Build(
            string id,
            string version,
            string description = null,
            NupkgDependency[] dependencies = null,
            NupkgLicense license = null)
        {
            description ??= $"Test package {id}";

            var nuspec = BuildNuspec(id, version, description, dependencies, license);

            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                // .nuspec
                var nuspecEntry = archive.CreateEntry($"{id}.nuspec", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(nuspecEntry.Open(), Encoding.UTF8))
                    writer.Write(nuspec);

                // Placeholder lib assemblies — required for dotnet restore to resolve the package
                foreach (var tfm in new[] { "net8.0", "net9.0", "net10.0" })
                {
                    var dllEntry = archive.CreateEntry($"lib/{tfm}/{id}.dll", CompressionLevel.Optimal);
                    using var dllStream = dllEntry.Open();
                    var placeholder = Encoding.UTF8.GetBytes($"placeholder-{id}-{version}");
                    dllStream.Write(placeholder, 0, placeholder.Length);
                }

                // If a file license is specified, embed the license file in the .nupkg
                if ((license?.Type == NupkgLicenseType.File || license?.Type == NupkgLicenseType.FileWithDeprecatedUrl)
                    && license.FileContent != null)
                {
                    var licenseEntry = archive.CreateEntry(license.FilePath, CompressionLevel.Optimal);
                    using var licenseStream = licenseEntry.Open();
                    licenseStream.Write(license.FileContent, 0, license.FileContent.Length);
                }

                // [Content_Types].xml
                var contentTypesEntry = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(contentTypesEntry.Open(), Encoding.UTF8))
                    writer.Write(ContentTypesXml);
            }

            return ms.ToArray();
        }

        private static string BuildNuspec(
            string id,
            string version,
            string description,
            NupkgDependency[] dependencies,
            NupkgLicense license)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">");
            sb.AppendLine("  <metadata>");
            sb.AppendLine($"    <id>{id}</id>");
            sb.AppendLine($"    <version>{version}</version>");
            sb.AppendLine("    <authors>CycloneDX E2E Tests</authors>");
            sb.AppendLine($"    <description>{description}</description>");

            if (license != null)
            {
                switch (license.Type)
                {
                    case NupkgLicenseType.Expression:
                        sb.AppendLine($"    <license type=\"expression\">{license.Expression}</license>");
                        break;
                    case NupkgLicenseType.File:
                        sb.AppendLine($"    <license type=\"file\">{license.FilePath}</license>");
                        break;
                    case NupkgLicenseType.FileWithDeprecatedUrl:
                        // Mirrors what `dotnet pack` does: emits both <license type="file"> and
                        // the NuGet deprecation stub URL so consumers can test the filtering logic.
                        sb.AppendLine($"    <license type=\"file\">{license.FilePath}</license>");
                        sb.AppendLine($"    <licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>");
                        break;
                    case NupkgLicenseType.Url:
                        sb.AppendLine($"    <licenseUrl>{license.Url}</licenseUrl>");
                        break;
                }
            }

            if (dependencies != null && dependencies.Length > 0)
            {
                sb.AppendLine("    <dependencies>");
                foreach (var tfm in new[] { "net8.0", "net9.0", "net10.0" })
                {
                    sb.AppendLine($"      <group targetFramework=\"{tfm}\">");
                    foreach (var dep in dependencies)
                    {
                        sb.AppendLine($"        <dependency id=\"{dep.Id}\" version=\"{dep.Version}\" />");
                    }
                    sb.AppendLine("      </group>");
                }
                sb.AppendLine("    </dependencies>");
            }

            sb.AppendLine("  </metadata>");
            sb.AppendLine("</package>");
            return sb.ToString();
        }

        private const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"nuspec\" ContentType=\"application/octet\" />" +
            "<Default Extension=\"dll\" ContentType=\"application/octet\" />" +
            "</Types>";
    }

    internal sealed class NupkgDependency
    {
        public string Id { get; }
        public string Version { get; }

        public NupkgDependency(string id, string version)
        {
            Id = id;
            Version = version;
        }
    }

    internal enum NupkgLicenseType { Expression, File, FileWithDeprecatedUrl, Url }

    internal sealed class NupkgLicense
    {
        public NupkgLicenseType Type { get; private set; }
        public string Expression { get; private set; }
        public string FilePath { get; private set; }
        public byte[] FileContent { get; private set; }
        public string Url { get; private set; }

        public static NupkgLicense Spdx(string expression) =>
            new NupkgLicense { Type = NupkgLicenseType.Expression, Expression = expression };

        public static NupkgLicense File(string path, byte[] content) =>
            new NupkgLicense { Type = NupkgLicenseType.File, FilePath = path, FileContent = content };

        /// <summary>
        /// Mirrors real NuGet pack output: <c>&lt;license type="file"&gt;</c> plus the auto-inserted
        /// <c>&lt;licenseUrl&gt;https://aka.ms/deprecateLicenseUrl&lt;/licenseUrl&gt;</c> stub.
        /// </summary>
        public static NupkgLicense FileWithDeprecatedUrl(string path, byte[] content) =>
            new NupkgLicense { Type = NupkgLicenseType.FileWithDeprecatedUrl, FilePath = path, FileContent = content };

        public static NupkgLicense LicenseUrl(string url) =>
            new NupkgLicense { Type = NupkgLicenseType.Url, Url = url };
    }
}
