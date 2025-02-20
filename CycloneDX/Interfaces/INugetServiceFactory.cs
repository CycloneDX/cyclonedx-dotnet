using System.Collections.Generic;
using System.IO.Abstractions;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Interfaces
{
    public interface INugetServiceFactory
    {
        INugetService Create(RunOptions option, IFileSystem fileSystem, IGithubService githubService, List<string> packageCachePaths);
    }
}
