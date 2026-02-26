using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;
using static CycloneDX.Models.Component;

namespace CycloneDX.Tests.FunctionalTests
{
    public class Issue758
    {
        readonly INugetServiceFactory nugetServiceFactory;

        public Issue758()
        {
            var mockNugetService = new Mock<INugetService>();
            mockNugetService.Setup(s => s.GetComponentAsync(
                    It.Is<DotnetDependency>(dep =>
                        dep.Name == "User.Dependent" &&
                        dep.Version == "1.0.0")))
                .Returns(Task.FromResult
                    (new Component {
                        Name = "User.Dependent",
                        Version = "1.0.0",
                        Type = Classification.Library,
                        BomRef = "pkg:nuget/User.Dependent@1.0.0"
                    }));

            var nugetService = mockNugetService.Object;

            var mockNugetServiceFactory = new Mock<INugetServiceFactory>();
            mockNugetServiceFactory.Setup(s => s.Create(
                It.IsAny<RunOptions>(),
                It.IsAny<IFileSystem>(),
                It.IsAny<IGithubService>(),
                It.IsAny<List<string>>()))
                .Returns(nugetService);

            nugetServiceFactory = mockNugetServiceFactory.Object;
        }


        // E2E counterpart: CycloneDX.E2ETests.ProjectReferencesTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task Issue758_BaseCase()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "Issue-758.json"));
            var options = new RunOptions
            {
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options, nugetServiceFactory);

            Assert.True(bom.Components.Count == 1);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "User.Dependent", true) == 0 && c.Version == "1.0.0");
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "User.Dependency", true) == 0 );
        }

        // E2E counterpart: CycloneDX.E2ETests.ProjectReferencesTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task Issue758_IPR()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "Issue-758.json"));
            var options = new RunOptions
            {
                includeProjectReferences = true
            };

            var bom = await FunctionalTestHelper.Test(assetsJson, options, nugetServiceFactory);

            Assert.True(bom.Components.Count == 2);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "User.Dependent", true) == 0 && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "User.Dependency", true) == 0 && c.Version == "1.0.0");
        }
    }
}
