using System.Threading.Tasks;

namespace CycloneDX.Interfaces
{
    public interface IXmlSigner
    {
        public Task SignAsync(string keyFile, string bomContent);
    }
}
