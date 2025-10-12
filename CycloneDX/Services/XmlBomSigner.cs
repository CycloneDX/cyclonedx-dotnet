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
    public class InvalidSigningKeyException : Exception
    {
        public InvalidSigningKeyException() : base()
        {
        }

        public InvalidSigningKeyException(string message) : base(message)
        {
        }

        public InvalidSigningKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

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
            signedBom.ComputeSignature();

            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new RSAKeyValue(rsa));
            signedBom.KeyInfo = keyInfo;

            var signature = signedBom.GetXml();
            bom.DocumentElement!.AppendChild(bom.ImportNode(signature, true));

            var stringBuilder = new StringBuilder();
            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                OmitXmlDeclaration = false,
                Encoding = new UTF8Encoding(false)
            };

            using (var xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings))
            {
                bom.Save(xmlWriter);
            }

            return stringBuilder.ToString();
        }
    }
}
