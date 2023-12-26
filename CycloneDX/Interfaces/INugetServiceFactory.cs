using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Interfaces
{
    public interface INugetServiceFactory
    {
        INugetService Create(RunOptions option, IFileSystem fileSystem, IGithubService githubService, List<string> packageCachePaths);
    }
}
