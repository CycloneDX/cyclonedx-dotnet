using System.Collections.Generic;
using System.IO.Abstractions;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace CycloneDX.Services
{
    public class NugetV3ServiceFactory : INugetServiceFactory
    {
        static readonly ILogger NullLogger = new NullLogger();

        public INugetService Create(RunOptions option, IFileSystem fileSystem, IGithubService githubService, List<string> packageCachePaths )
        {
            var nugetInput = NugetInputFactory.Create(option.baseUrl, option.baseUrlUserName, option.baseUrlUSP, option.isPasswordClearText);
            var sourceRepository = SetupNugetRepository(nugetInput);
            return Create(sourceRepository, fileSystem, githubService, packageCachePaths, option.disableHashComputation);
        }

        public static INugetService Create(SourceRepository sourceRepository,
            IFileSystem fileSystem, IGithubService githubService,
            List<string> packageCachePaths, bool disableHashComputation)
        {
            return new NugetV3Service(sourceRepository, fileSystem, packageCachePaths,
                githubService, NullLogger, disableHashComputation);
        }

        public static SourceRepository SetupNugetRepository(NugetInputModel nugetInput)
        {
            if (nugetInput == null || string.IsNullOrEmpty(nugetInput.nugetFeedUrl) ||
                string.IsNullOrEmpty(nugetInput.nugetUsername) || string.IsNullOrEmpty(nugetInput.nugetPassword))
            {
                return CreateDefaultNugetRepository(nugetInput);
            }

            var packageSource = GetPackageSourceWithCredentials(nugetInput);
            return Repository.Factory.GetCoreV3(packageSource);
        }

        public static SourceRepository CreateDefaultNugetRepository(NugetInputModel nugetInput) =>
            Repository.Factory.GetCoreV3(nugetInput?.nugetFeedUrl ?? "https://api.nuget.org/v3/index.json");

        static PackageSource GetPackageSourceWithCredentials(NugetInputModel nugetInput)
        {
            var packageSource = new PackageSource(nugetInput.nugetFeedUrl)
            {
                Credentials = new(nugetInput.nugetFeedUrl, nugetInput.nugetUsername,
                    nugetInput.nugetPassword,
                    nugetInput.IsPasswordClearText, null)
            };

            return packageSource;
        }
    }
}
