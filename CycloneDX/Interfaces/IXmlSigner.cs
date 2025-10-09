using System.Threading.Tasks;

namespace CycloneDX.Interfaces
{
    public interface IXmlSigner
    {
        public Task<string> SignAsync(string keyFile, string bomContent);
    }
}
