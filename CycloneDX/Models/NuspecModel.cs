using NuGet.Packaging;

namespace CycloneDX.Models
{
    public class NuspecModel
    {
        public NuspecReader NuspecReader { get; set; }
        public byte[] hashBytes { get; set; }

        public NuspecModel()
        {
            NuspecReader = null;
            hashBytes = null;
        }
    }
}
