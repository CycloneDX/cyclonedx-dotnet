using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using JetBrains.Annotations;
using NuGet.Common;

namespace CycloneDX.Services
{
    public class NugetV3ServiceFactory : INugetServiceFactory
    {
        public INugetService Create(RunOptions option, IFileSystem fileSystem, IGithubService githubService, List<string> packageCachePaths, HashSet<NugetInputModel> nugetInputModels)
        {
            var nugetLogger = new NuGet.Common.NullLogger();
            var nugetInput = NugetInputFactory.Create(option.baseUrl, option.baseUrlUserName, option.baseUrlUSP, option.isPasswordClearText);

            if (nugetInputModels == null)
            {
                nugetInputModels = new HashSet<NugetInputModel>();
            }
            if (nugetInput != null)
            {
                nugetInputModels.Add(nugetInput);
            }
            if (!nugetInputModels.Any())
            {
                nugetInputModels.Add(new NugetInputModel("https://api.nuget.org/v3/index.json"));
            }
            return new NugetV3Service(nugetInputModels, fileSystem, packageCachePaths, githubService, nugetLogger, option.disableHashComputation);
        }
    }
}
