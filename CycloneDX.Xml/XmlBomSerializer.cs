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
using System.Linq;
using System.Xml.Linq;
using CycloneDX.Models;

namespace CycloneDX.Xml
{

    public static class XmlBomSerializer
    {
        public static string Serialize(Bom bom)
        {
            Contract.Requires(bom != null);

            // hacky work around for incomplete spec multi-version support, JSON defaults to v1.2
            XNamespace ns = "http://cyclonedx.org/schema/bom/" + bom.SpecVersion;
            var doc = new XDocument();
            doc.Declaration = new XDeclaration("1.0", "utf-8", null);

            var bomElement = (string.IsNullOrEmpty(bom.SerialNumber)) ? new XElement(ns + "bom", new XAttribute("version", bom.Version)) :
                new XElement(ns + "bom", new XAttribute("version", bom.Version), new XAttribute("serialNumber", bom.SerialNumber));

            if (bom.Metadata != null)
            {
                var meta = new XElement(ns + "metadata");
                if (bom.Metadata.Timestamp != null)
                {
                    meta.Add(new XElement(ns + "timestamp", bom.Metadata.Timestamp?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")));
                }

                if (bom.Metadata.Tools != null && bom.Metadata.Tools.Count > 0)
                {
                    var tools = new XElement(ns + "tools");
                    foreach (var tool in bom.Metadata.Tools)
                    {
                        tools.Add(SerializeTool(ns, tool));
                    }
                    meta.Add(tools);
                }

                if (bom.Metadata.Authors != null && bom.Metadata.Authors.Count > 0)
                {
                    var authors = new XElement(ns + "authors");
                    foreach (var author in bom.Metadata.Authors)
                    {
                        authors.Add(SerializeOrgnizationalContact(ns, "author", author));
                    }
                    meta.Add(authors);
                }

                if (bom.Metadata.Manufacture != null)
                {
                    meta.Add(SerializeOrganizationalEntity(ns, "manufacture", bom.Metadata.Manufacture));
                }

                if (bom.Metadata.Supplier != null)
                {
                    meta.Add(SerializeOrganizationalEntity(ns, "supplier", bom.Metadata.Supplier));
                }

                bomElement.Add(meta);
            }

            if (bom.Components != null)
            {
                var sortedComponents = bom.Components.ToList();
                sortedComponents.Sort();

                var com = new XElement(ns + "components");
                foreach (var component in sortedComponents)
                {
                    com.Add(SerializeComponent(ns, component));
                }
                bomElement.Add(com);
            }

            doc.Add(bomElement);

            using (var sw = new Utf8StringWriter())
            {
                doc.Save(sw);
                return sw.ToString();
            }
        }

        internal static XElement SerializeComponent(XNamespace ns, Component component)
        {
            var c = new XElement(ns + "component", new XAttribute("type", component.Type));

            if (component.BomRef != null) c.SetAttributeValue("bom-ref", component.BomRef);

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
            if (component.Hashes?.Count > 0)
            {
                var h = new XElement(ns + "hashes");
                foreach (var hash in component.Hashes)
                {
                    h.Add(SerializeHash(ns, hash));
                }
                c.Add(h);
            }
            if (component.Licenses != null && component.Licenses.Count > 0)
            {
                var l = new XElement(ns + "licenses");
                foreach (var componentLicense in component.Licenses)
                {
                    var license = componentLicense.License;
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

            if (component.Components?.Count() > 0)
            {
                var subcomponents = new XElement(ns + "components");
                foreach (var subcomponent in component.Components)
                {
                    subcomponents.Add(SerializeComponent(ns, subcomponent));
                }
                c.Add(subcomponents);
            }

            return c;
        }

        internal static XElement SerializeTool(XNamespace ns, Tool tool)
        {
            var toolElement = new XElement(ns + "tool");
            if (!string.IsNullOrEmpty(tool.Vendor))
            {
                toolElement.Add(new XElement(ns + "vendor", tool.Vendor));
            }
            
            if (!string.IsNullOrEmpty(tool.Name))
            {
                toolElement.Add(new XElement(ns + "name", tool.Name));
            }

            if (!string.IsNullOrEmpty(tool.Version))
            {
                toolElement.Add(new XElement(ns + "version", tool.Version));
            }

            if (tool.Hashes?.Count() > 0)
            {
                var hashesElement = new XElement(ns + "hashes");
                foreach (var hash in tool.Hashes)
                {
                    hashesElement.Add(SerializeHash(ns, hash));
                }
                toolElement.Add(hashesElement);
            }

            return toolElement;
        }

        internal static XElement SerializeHash(XNamespace ns, Hash hash)
        {
            var hashElement = new XElement(ns + "hash", hash.Content);
            hashElement.SetAttributeValue("alg", hash.Alg);
            return hashElement;
        }

        internal static XElement SerializeOrganizationalEntity(XNamespace ns, string elementName, OrganizationalEntity entity)
        {
            var entityElement = new XElement(ns + elementName);
            if (!string.IsNullOrEmpty(entity.Name))
            {
                entityElement.Add(new XElement(ns + "name", entity.Name));
            }
            
            if (entity.Url?.Count() > 0)
            foreach (var url in entity.Url)
            {
                entityElement.Add(new XElement(ns + "url", url));
            }

            if (entity.Contact?.Count() > 0)
            foreach (var contact in entity.Contact)
            {
                entityElement.Add(SerializeOrgnizationalContact(ns, "contact", contact));
            }

            return entityElement;
        }

        internal static XElement SerializeOrgnizationalContact(XNamespace ns, string elementName, OrganizationalContact contact)
        {
            var contactElement = new XElement(ns + elementName);
            if (!string.IsNullOrEmpty(contact.Name))
            {
                contactElement.Add(new XElement(ns + "name", contact.Name));
            }
            if (!string.IsNullOrEmpty(contact.Email))
            {
                contactElement.Add(new XElement(ns + "email", contact.Email));
            }
            if (!string.IsNullOrEmpty(contact.Phone))
            {
                contactElement.Add(new XElement(ns + "phone", contact.Phone));
            }
            return contactElement;
        }
    }
}
