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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using CycloneDX.Models;

namespace CycloneDX {
    /// <summary>
    /// Service to generate bill of materials
    /// </summary>
    public static class BomService
    {
        public static string CreateDocument(Bom bom, bool json)
        {
            if (json)
            {
                return CreateJsonDocument(bom);
            }
            else
            {
                return CreateXmlDocument(bom);
            }
        }

        /// <summary>
        /// Creates a CycloneDX BOM from the list of components
        /// </summary>
        /// <param name="components">Components that should be included in the BOM</param>
        /// <param name="noSerialNumber">Optionally omit the serial number from the resulting BOM</param>
        /// <returns>CycloneDX XDocument</returns>
        public static string CreateJsonDocument(Bom bom)
        {
            Contract.Requires(bom != null);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true,
            };

            var jsonBom = JsonSerializer.Serialize(bom, options);

            return jsonBom;
        }
        
        /// <summary>
        /// Creates a CycloneDX BOM from the list of components
        /// </summary>
        /// <param name="components">Components that should be included in the BOM</param>
        /// <param name="noSerialNumber">Optionally omit the serial number from the resulting BOM</param>
        /// <returns>CycloneDX XDocument</returns>
        public static string CreateXmlDocument(Bom bom)
        {
            Contract.Requires(bom != null);

            XNamespace ns = "http://cyclonedx.org/schema/bom/1.1";
            var doc = new XDocument();
            doc.Declaration = new XDeclaration("1.0", "utf-8", null);

            var bomElement = (string.IsNullOrEmpty(bom.SerialNumber)) ? new XElement(ns + "bom", new XAttribute("version", bom.Version)) :
                new XElement(ns + "bom", new XAttribute("version", bom.Version), new XAttribute("serialNumber", bom.SerialNumber));

            var sortedComponents = bom.Components.ToList();
            sortedComponents.Sort();

            var com = new XElement(ns + "components");
            foreach (var component in sortedComponents)
            {
                Console.WriteLine(component.Name);
                var c = new XElement(ns + "component", new XAttribute("type", component.Type));
                if (!string.IsNullOrEmpty(component.Group))
                {
                    c.Add(new XElement(ns + "group", component.Group));
                }
                if (!string.IsNullOrEmpty(component.Name))
                {
                    c.Add(new XElement(ns + "name", component.Name));
                }
                if (!string.IsNullOrEmpty(component.Version))
                {
                    c.Add(new XElement(ns + "version", component.Version));
                }
                if (!string.IsNullOrEmpty(component.Description))
                {
                    c.Add(new XElement(ns + "description", new XCData(component.Description)));
                }
                if (!string.IsNullOrEmpty(component.Scope))
                {
                    c.Add(new XElement(ns + "scope", component.Scope));
                }
                if (component.Hashes != null && component.Hashes.Count > 0)
                {
                    var h = new XElement(ns + "hashes");
                    foreach (var hash in component.Hashes)
                    {
                        h.Add(new XElement(ns + "hash", hash.value, new XAttribute("alg", Models.AlgorithmExtensions.GetXmlString(hash.algorithm))));
                    }
                }
                if (component.Licenses != null && component.Licenses.Count > 0)
                {
                    var l = new XElement(ns + "licenses");
                    foreach (var license in component.Licenses)
                    {
                        if (license.Id != null)
                        {
                            l.Add(new XElement(ns + "license", new XElement(ns + "id", license.Id)));
                        }
                        else if (license.Name != null)
                        {
                            l.Add(new XElement(ns + "license", new XElement(ns + "name", license.Name)));
                        }
                        else if (license.Url != null)
                        {
                            l.Add(new XElement(ns + "license", new XElement(ns + "url", license.Url)));
                        }
                    }
                    c.Add(l);
                }
                if (!string.IsNullOrEmpty(component.Copyright))
                {
                    c.Add(new XElement(ns + "copyright", component.Copyright));
                }
                if (!string.IsNullOrEmpty(component.Purl))
                {
                    c.Add(new XElement(ns + "purl", component.Purl));
                }
                if (component.ExternalReferences != null && component.ExternalReferences.Count > 0)
                {
                    var externalReferences = new XElement(ns + "externalReferences");
                    foreach (var externalReference in component.ExternalReferences)
                    {
                        externalReferences.Add(new XElement(ns + "reference", new XAttribute("type", externalReference.Type), new XElement(ns + "url", externalReference.Url)));
                    }
                    c.Add(externalReferences);
                }

                com.Add(c);
            }
            bomElement.Add(com);

            doc.Add(bomElement);

            using (var sw = new Utf8StringWriter())
            {
                doc.Save(sw);
                return sw.ToString();
            }
        }
    }
}
