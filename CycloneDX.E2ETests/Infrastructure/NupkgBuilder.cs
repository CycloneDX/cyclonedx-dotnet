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
            NupkgDependency[] dependencies = null)
        {
            description ??= $"Test package {id}";

            var nuspec = BuildNuspec(id, version, description, dependencies);

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
            NupkgDependency[] dependencies)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">");
            sb.AppendLine("  <metadata>");
            sb.AppendLine($"    <id>{id}</id>");
            sb.AppendLine($"    <version>{version}</version>");
            sb.AppendLine("    <authors>CycloneDX E2E Tests</authors>");
            sb.AppendLine($"    <description>{description}</description>");

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
}
