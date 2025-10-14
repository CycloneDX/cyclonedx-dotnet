using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Xunit;
using System.Threading.Tasks;
using System.Xml;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class XmlBomSignerTests
    {

        private const string TestBom = """
                                       <?xml version="1.0" encoding="UTF-8"?>
                                       <bom xmlns="http://cyclonedx.org/schema/bom/1.6"
                                            serialNumber="urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79"
                                            version="1">
                                           <components>
                                               <component type="library">
                                                   <publisher>Apache</publisher>
                                                   <group>org.apache.tomcat</group>
                                                   <name>tomcat-catalina</name>
                                                   <version>9.0.14</version>
                                                   <hashes>
                                                       <hash alg="MD5">3942447fac867ae5cdb3229b658f4d48</hash>
                                                       <hash alg="SHA-1">e6b1000b94e835ffd37f4c6dcbdad43f4b48a02a</hash>
                                                       <hash alg="SHA-256">f498a8ff2dd007e29c2074f5e4b01a9a01775c3ff3aeaf6906ea503bc5791b7b</hash>
                                                       <hash alg="SHA-512">e8f33e424f3f4ed6db76a482fde1a5298970e442c531729119e37991884bdffab4f9426b7ee11fccd074eeda0634d71697d6f88a460dce0ac8d627a29f7d1282</hash>
                                                   </hashes>
                                                   <licenses>
                                                       <license>
                                                           <id>Apache-2.0</id>
                                                       </license>
                                                   </licenses>
                                                   <purl>pkg:maven/org.apache.tomcat/tomcat-catalina@9.0.14</purl>
                                               </component>
                                           </components>
                                       </bom>
                                       """;

        [Fact]
        public async Task SignAsyncShouldAddXmlSignatureElement()
        {
            var signer = new XmlBomSigner();
            var tempKeyFile = Path.GetTempFileName();

            using var rsa = RSA.Create();
            var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            await File.WriteAllTextAsync(tempKeyFile, privateKeyPem);

            var signedXml = await signer.SignAsync(tempKeyFile, TestBom);

            var doc = new XmlDocument();
            doc.LoadXml(signedXml);

            var signatureNode = doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
            Assert.True(signatureNode.Count == 1, "Expected a Signature Element in BOM after Signing.");
        }

        [Fact]
        public async Task SignAsyncThrowsOnMalformedPrivateKes()
        {
            var signer = new XmlBomSigner();
            var tempKeyFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempKeyFile, "Not a valid PEM Key");

            await Assert.ThrowsAsync<InvalidSigningKeyException>(() =>
                signer.SignAsync(tempKeyFile, TestBom));
        }
    }
}
