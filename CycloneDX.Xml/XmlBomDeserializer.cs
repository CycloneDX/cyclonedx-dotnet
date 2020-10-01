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
using System.Xml;
using CycloneDX.Models;

namespace CycloneDX.Xml
{

    public static class XmlBomDeserializer
    {
        public static Bom Deserialize(string xmlBom)
        {
            Contract.Requires(xmlBom != null);

            var bom = new Bom();
            var doc = new XmlDocument();
            doc.LoadXml(xmlBom);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("cdx", doc.DocumentElement.Attributes["xmlns"].InnerText);

            bom.SpecVersion = doc.DocumentElement.Attributes["xmlns"]
                .InnerText
                .Replace("http://cyclonedx.org/schema/bom/", "");
            bom.Version = int.Parse(doc.DocumentElement.Attributes["version"]?.InnerText);
            bom.SerialNumber = doc.DocumentElement.Attributes["serialNumber"]?.InnerText;

            var xmlMetadataNode = doc.SelectSingleNode("/cdx:bom/cdx:metadata", nsmgr);
            if (xmlMetadataNode != null)
            {
                bom.Metadata = new Metadata();
                var xmlAuthorNodes = xmlMetadataNode.SelectNodes("cdx:authors/cdx:author", nsmgr);
                for (var i=0; i<xmlAuthorNodes.Count; i++)
                {
                    bom.Metadata.Authors.Add(GetOrganizationalContact(xmlAuthorNodes[i]));
                }
            }

            var xmlComponentNodes = doc.SelectNodes("/cdx:bom/cdx:components/cdx:component", nsmgr);
            for (var i=0; i<xmlComponentNodes.Count; i++)
            {
                bom.Components.Add(GetComponent(xmlComponentNodes[i], nsmgr));
            }

            return bom;
        }

        private static Component GetComponent(XmlNode componentXmlNode, XmlNamespaceManager nsmgr)
        {
            var component = new Component();
            if (componentXmlNode == null) return component;

            component.Publisher = componentXmlNode.Attributes["publisher"]?.InnerText;
            component.Group = componentXmlNode.Attributes["group"]?.InnerText;
            component.Type = componentXmlNode.Attributes["type"]?.InnerText;
            component.Name = componentXmlNode["name"]?.InnerText;
            component.Version = componentXmlNode["version"]?.InnerText;
            component.Description = componentXmlNode["description"]?.InnerText;
            component.Scope = componentXmlNode["scope"]?.InnerText;
            
            var licenseXmlNodes = componentXmlNode.SelectNodes("cdx:licenses/cdx:license", nsmgr);
            for (var i=0; i<licenseXmlNodes.Count; i++)
            {
                var licenseXmlNode = licenseXmlNodes[i];
                component.Licenses.Add(GetLicense(licenseXmlNode));
            }

            component.Copyright = componentXmlNode["copyright"]?.InnerText;
            component.Purl = componentXmlNode["purl"]?.InnerText;
            
            var externalReferenceXmlNodes = componentXmlNode.SelectNodes("cdx:externalReferences/cdx:reference", nsmgr);
            for (var i=0; i<externalReferenceXmlNodes.Count; i++)
            {
                var externalReferenceXmlNode = externalReferenceXmlNodes[i];
                component.ExternalReferences.Add(GetExternalReference(externalReferenceXmlNode));
            }

            return component;
        }

        private static ComponentLicense GetLicense(XmlNode licenseXmlNode)
        {
            var license = new License
            {
                Id = licenseXmlNode["id"]?.InnerText,
                Name = licenseXmlNode["name"]?.InnerText,
                Url = licenseXmlNode["url"]?.InnerText
            };

            return new ComponentLicense
            {
                License = license
            };
        }

        private static ExternalReference GetExternalReference(XmlNode externalReferenceXmlNode)
        {
            var externalReference = new ExternalReference
            {
                Type = externalReferenceXmlNode.Attributes["type"]?.InnerText,
                Url = externalReferenceXmlNode["url"]?.InnerText
            };

            return externalReference;
        }

        private static OrganizationalContact GetOrganizationalContact(XmlNode organizationalContactXmlNode)
        {
            var organizationalContact = new OrganizationalContact
            {
                Email = organizationalContactXmlNode["email"]?.InnerText,
                Name = organizationalContactXmlNode["name"]?.InnerText,
                Phone = organizationalContactXmlNode["phone"]?.InnerText
            };

            return organizationalContact;
        }
    }
}
