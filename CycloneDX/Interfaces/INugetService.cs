using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX.Interfaces
{
    public interface INugetService
    {
        Task<Component> GetComponentAsync(string name, string version, Component.ComponentScope? scope);
        Task<Component> GetComponentAsync(NugetPackage nugetPackage);
    }
}
