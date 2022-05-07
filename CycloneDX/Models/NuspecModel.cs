using NuGet.Packaging;

namespace CycloneDX.Models
{
    public class NuspecModel
    {
        public NuspecReader nuspecReader { get; set; }
        public byte[] hashBytes { get; set; }

        public NuspecModel()
        {
            nuspecReader = null;
            hashBytes = null;
        }
    }
}
