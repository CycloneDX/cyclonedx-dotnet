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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CycloneDX.Models;

namespace CycloneDX {
    /// <summary>
    /// Service to generate bill of materials
    /// </summary>
    public static class BomService
    {
        /// <summary>
        /// Creates a CycloneDX BOM from the list of components
        /// </summary>
        /// <param name="components">Components that should be included in the BOM</param>
        /// <param name="noSerialNumber">Optionally omit the serial number from the resulting BOM</param>
        /// <returns>CycloneDX XDocument</returns>
        public static XDocument CreateXmlDocument(HashSet<Component> components, bool noSerialNumber = false)
        {
            XNamespace ns = "http://cyclonedx.org/schema/bom/1.1";
            var doc = new XDocument();
            var serialNumber = "urn:uuid:" + System.Guid.NewGuid().ToString();
            doc.Declaration = new XDeclaration("1.0", "utf-8", null);

            var bom = (noSerialNumber) ? new XElement(ns + "bom", new XAttribute("version", "1")) :
                new XElement(ns + "bom", new XAttribute("version", "1"), new XAttribute("serialNumber", serialNumber));

            var sortedComponents = components.ToList();
            sortedComponents.Sort();

            var com = new XElement(ns + "components");
            foreach (var component in sortedComponents)
            {
                Console.WriteLine(component.Name);
                var c = new XElement(ns + "component", new XAttribute("type", "library"));
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
                if (!string.IsNullOrEmpty(component.Cpe))
                {
                    c.Add(new XElement(ns + "cpe", component.Cpe));
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
            bom.Add(com);
            doc.Add(bom);
            return doc;
        }
    }
}
