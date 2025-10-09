using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
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

    public class XmlSigner : IXmlSigner
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
            bom.Load(bomContent);

            var signedBom = new SignedXml(bom);
            signedBom.SigningKey = rsa;
            var reference = new Reference();
            var envelope = new XmlDsigEnvelopedSignatureTransform();

            reference.AddTransform(envelope);
            signedBom.AddReference(reference);
            signedBom.ComputeSignature();

            var signature = signedBom.GetXml();
            bom.DocumentElement!.AppendChild(bom.ImportNode(signature, true));

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter,
                new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });

            bom.WriteTo(xmlWriter);
            await xmlWriter.FlushAsync().ConfigureAwait(false);

            return stringWriter.ToString();
        }
    }
}
