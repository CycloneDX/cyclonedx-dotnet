// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using CycloneDX.Interfaces;
using System.Threading.Tasks;
using System.Xml;

namespace CycloneDX.Services
{
    public class InvalidSigningKeyException(string message, Exception innerException)
        : Exception(message, innerException);

    public class XmlBomSigner : IBomSigner
    {
        public async Task<string> SignAsync(string keyFile, string bomContent)
        {
            var privateKey = await File.ReadAllTextAsync(keyFile).ConfigureAwait(false);
            using var rsa = RSA.Create();

            try
            {
                rsa.ImportFromPem(privateKey);
            }
            catch (ArgumentException e)
            {
                throw new InvalidSigningKeyException("The provided Signing Key is malformed", e);
            }

            var bom = new XmlDocument();
            bom.PreserveWhitespace = true;
            bomContent = bomContent.TrimStart('\uFEFF', '\u200B');
            bom.LoadXml(bomContent);

            var signedBom = new SignedXml(bom);
            signedBom.SigningKey = rsa;
            var reference = new Reference("");
            var envelope = new XmlDsigEnvelopedSignatureTransform();

            reference.AddTransform(envelope);
            reference.AddTransform(new XmlDsigC14NTransform());
            signedBom.AddReference(reference);

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new RSAKeyValue(rsa));
            signedBom.KeyInfo = keyInfo;

            signedBom.ComputeSignature();

            var signature = signedBom.GetXml();
            bom.DocumentElement!.AppendChild(bom.ImportNode(signature, true));

            using var memoryStream = new MemoryStream();
            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                OmitXmlDeclaration = false,
                Encoding = new UTF8Encoding(false),
                Async = true
            };

            using (var xmlWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
            {
                bom.Save(xmlWriter);
                await xmlWriter.FlushAsync();
            }

            var utf8String = Encoding.UTF8.GetString(memoryStream.ToArray());
            return utf8String;
        }
    }
}
