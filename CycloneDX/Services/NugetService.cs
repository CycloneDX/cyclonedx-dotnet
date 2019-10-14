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
using Microsoft.Extensions.Logging;
using CycloneDX.Extensions;
using CycloneDX.Model;

namespace CycloneDX.Services
{
    public class NugetService
    {
        private string _baseUrl = "https://api.nuget.org/v3-flatcontainer/";
        private ILogger _logger;
        private HttpClient _httpClient;

        public NugetService(
            ILogger logger = null,
            HttpClient httpClient = null
        )
        {
            _logger = logger;
            _httpClient = httpClient == null ? new HttpClient() : httpClient;
        }

        /*
         * Retrieves the specified Component from NuGet.
         */
        public async Task<Component> GetComponent(string name, string version) {
            var url = _baseUrl + name + "/" + version + "/" + name + ".nuspec";
            var doc = await _httpClient.GetXmlAsync(url);
            var component = new Component
            {
                Name = name,
                Version = version,
                Purl = Utils.GeneratePackageUrl(name, version)
            };
            var root = doc.DocumentElement;
            var metadata = root.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']");
            component.Publisher = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'authors']");
            component.Copyright = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'copyright']");
            var title = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'title']");
            var summary = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'summary']");
            var description = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'description']");
            if (summary != null) {
                component.Description = summary;
            } else if (description != null) {
                component.Description = description;
            } else if (title != null) {
                component.Description = title;
            }

            // Utilize the new license expression field present in more recent packages
            // TODO: Need to have more robust parsing to support composite expressions seen in (https://github.com/NuGet/Home/wiki/Packaging-License-within-the-nupkg#project-properties)
            var licenseNode = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'license']");
            var licenseUrlNode = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'licenseUrl']");
            if (licenseNode?.Attributes["type"].Value == "expression") {
                var licenses = licenseNode.FirstChild.Value
                    .Replace("AND", ";")
                    .Replace("OR", ";")
                    .Replace("WITH", ";")
                    .Replace("+", "")
                    .Split(';').ToList();
                foreach (var license in licenses) {
                    component.Licenses.Add(new Model.License {
                        Id = license.Trim(),
                        Name = license.Trim()
                    });
                }
            } else if (licenseUrlNode != null) {
                var licenseUrl = licenseUrlNode.FirstChild.Value;
                component.Licenses.Add(new Model.License {
                    Url = licenseUrl.Trim()
                });
            }

            var projectUrl = GetNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'projectUrl']");
            if (projectUrl != null) {
                var externalReference = new Model.ExternalReference();
                externalReference.Type = Model.ExternalReference.WEBSITE;
                externalReference.Url = projectUrl;
                component.ExternalReferences.Add(externalReference);
            }

            return component;

            // if (followTransitive) {
            //     var dependencies = metadata.SelectNodes("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'dependencies']/*[local-name() = 'dependency']");
            //     foreach (XmlNode dependency in dependencies) {
            //         var id = dependency.Attributes["id"];
            //         var version = dependency.Attributes["version"];
            //         if (id != null && version != null) {
            //             var transitive = new Model.Component();
            //             transitive.Name = id.Value;
            //             transitive.Version = version.Value;
            //             transitive.Purl = Utils.generatePackageUrl(transitive.Name, transitive.Version);
            //             await RetrieveExtendedNugetAttributes(transitive, false);
            //         }
            //     }
            // }
        }

        /*
         * Helper method which performs null checking when querying for the value of an XML node.
         */
        private static string GetNodeValue(XmlNode xmlNode, string xpath) {
            var node = xmlNode.SelectSingleNode(xpath);
            if (node != null && node.FirstChild != null) {
                return node.FirstChild.Value;
            }
            return null;
        }

    }
}