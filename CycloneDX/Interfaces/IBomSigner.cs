using System.Threading.Tasks;

namespace CycloneDX.Interfaces
{
    public interface IBomSigner
    {
        public Task<string> SignAsync(string keyFile, string bomContent);
    }
}
