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

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Builds minimal valid .nupkg files in memory.
    /// A .nupkg is a ZIP containing a .nuspec and optionally content files.
    /// We add a small text file so the package has a real (stable) hash.
    /// </summary>
    internal static class NupkgBuilder
    {
        /// <summary>
        /// Creates a .nupkg byte array for a package with the given id, version,
        /// optional dependencies, and a stable content file so SHA-512 hashing works.
        /// </summary>
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
                // .nuspec entry
                var nuspecEntry = archive.CreateEntry($"{id}.nuspec", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(nuspecEntry.Open(), Encoding.UTF8))
                    writer.Write(nuspec);

                // Stable content file — gives the package a real, reproducible hash
                var contentEntry = archive.CreateEntry($"content/marker.txt", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(contentEntry.Open(), Encoding.UTF8))
                    writer.Write($"{id}@{version}");

                // Minimal [Content_Types].xml required by NuGet clients
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
                sb.AppendLine("      <group targetFramework=\"net8.0\">");
                foreach (var dep in dependencies)
                    sb.AppendLine($"        <dependency id=\"{dep.Id}\" version=\"{dep.Version}\" />");
                sb.AppendLine("      </group>");
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
            "<Default Extension=\"txt\" ContentType=\"text/plain\" />" +
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
