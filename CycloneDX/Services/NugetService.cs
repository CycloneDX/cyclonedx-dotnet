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

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using CycloneDX.Extensions;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public interface INugetService
    {
        Task<Component> GetComponentAsync(string name, string version);
        Task<Component> GetComponentAsync(NugetPackage nugetPackage);
    }

    public class NugetService : INugetService
    {
        private string _baseUrl;
        private HttpClient _httpClient;

        public NugetService(HttpClient httpClient, string baseUrl = null)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl == null ? "https://api.nuget.org/v3-flatcontainer/" : baseUrl;
        }

        /// <summary>
        /// Retrieves the specified component from NuGet.
        /// </summary>
        /// <param name="name">NuGet package name</param>
        /// <param name="version">Package version</param>
        /// <returns></returns>
        public async Task<Component> GetComponentAsync(string name, string version)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version)) return null;

            Console.WriteLine("Retrieving " + name + " " + version);

            var component = new Component
            {
                Name = name,
                Version = version,
                Purl = Utils.GeneratePackageUrl(name, version)
            };

            var url = _baseUrl + name + "/" + version + "/" + name + ".nuspec";
            var doc = await _httpClient.GetXmlAsync(url);

            if (doc == null) return component;

            var root = doc.DocumentElement;
            var metadata = root.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']");
            component.Publisher = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'authors']");
            component.Copyright = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'copyright']");
            var title = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'title']");
            var summary = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'summary']");
            var description = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'description']");
            if (summary != null)
            {
                component.Description = summary;
            }
            else if (description != null)
            {
                component.Description = description;
            }
            else if (title != null)
            {
                component.Description = title;
            }

            // Utilize the new license expression field present in more recent packages
            // TODO: Need to have more robust parsing to support composite expressions seen in (https://github.com/NuGet/Home/wiki/Packaging-License-within-the-nupkg#project-properties)
            var licenseNode = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'license']");
            var licenseUrlNode = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'licenseUrl']");
            if (licenseNode?.Attributes["type"].Value == "expression")
            {
                var licenses = licenseNode.FirstChild.Value
                    .Replace("AND", ";")
                    .Replace("OR", ";")
                    .Replace("WITH", ";")
                    .Replace("+", "")
                    .Split(';').ToList();
                foreach (var license in licenses)
                {
                    component.Licenses.Add(new Models.License
                    {
                        Id = license.Trim(),
                        Name = license.Trim()
                    });
                }
            }
            else if (licenseUrlNode != null)
            {
                var licenseUrl = licenseUrlNode.FirstChild.Value;
                component.Licenses.Add(new Models.License
                {
                    Url = licenseUrl.Trim()
                });
            }

            var projectUrl = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'projectUrl']");
            if (projectUrl != null)
            {
                var externalReference = new Models.ExternalReference();
                externalReference.Type = Models.ExternalReference.WEBSITE;
                externalReference.Url = projectUrl;
                component.ExternalReferences.Add(externalReference);
            }

            var dependencies = metadata.SelectNodes("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'dependencies']/*[local-name() = 'dependency']");
            foreach (XmlNode dependency in dependencies) {
                var dependencyName = dependency.Attributes["id"];
                var dependencyVersion = dependency.Attributes["version"];
                if (dependencyName != null && dependencyVersion != null) {
                    var nugetDependency = new NugetPackage {
                        Name = dependencyName.Value,
                        Version = dependencyVersion.Value,
                    };
                    component.Dependencies.Add(nugetDependency);
                }
            }

            return component;
        }

        /// <summary>
        /// Retrieves the specified component from NuGet.
        /// </summary>
        /// <param name="package">NuGet package</param>
        /// <returns></returns>
        public async Task<Component> GetComponentAsync(NugetPackage package)
        {
            return await GetComponentAsync(package.Name, package.Version);
        }

        /// <summary>
        /// Helper method which performs null checking when querying for the value of an XML node.
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <param name="xpath"></param>
        /// <returns></returns>
        private static string GetNodeValue(XmlNode xmlNode, string xpath)
        {
            var node = xmlNode.SelectSingleNode(xpath);
            if (node != null && node.FirstChild != null)
            {
                return node.FirstChild.Value;
            }
            return null;
        }

    }
}