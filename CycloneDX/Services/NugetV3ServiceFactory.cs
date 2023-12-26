using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class NugetV3ServiceFactory : INugetServiceFactory
    {
        public INugetService Create(RunOptions option, IFileSystem fileSystem, IGithubService githubService, List<string> packageCachePaths )
        {
            var nugetLogger = new NuGet.Common.NullLogger();
            var nugetInput = NugetInputFactory.Create(option.baseUrl, option.baseUrlUserName, option.baseUrlUSP, option.isPasswordClearText);
            return new NugetV3Service(nugetInput, fileSystem, packageCachePaths, githubService, nugetLogger, option.disableHashComputation);
        }
    }
}
