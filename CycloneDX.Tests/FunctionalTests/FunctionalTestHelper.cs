using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;
using static CycloneDX.Models.Component;

namespace CycloneDX.Tests.FunctionalTests
{
    public static class FunctionalTestHelper
    {
        private static INugetServiceFactory CreateMockNugetServiceFactory()
        {
            var mockNugetService = new Mock<INugetService>();
            mockNugetService.Setup(s => s.GetComponentAsync(
                    It.IsAny<DotnetDependency>()))
                .Returns((DotnetDependency dep) => Task.FromResult
                    (new Component
                    {
                        Name = dep.Name,
                        Version = dep.Version,
                        Type = Classification.Library,
                        BomRef = $"pkg:nuget/{dep.Name}@{dep.Version}",
                        Scope = dep.Scope
                    })) ;

            var nugetService = mockNugetService.Object;

            var mockNugetServiceFactory = new Mock<INugetServiceFactory>();
            mockNugetServiceFactory.Setup(s => s.Create(
                It.IsAny<RunOptions>(),
                It.IsAny<IFileSystem>(),
                It.IsAny<IGithubService>(),
                It.IsAny<List<string>>()))
                .Returns(nugetService);

            return mockNugetServiceFactory.Object;
        }


        public static async Task<Bom> Test(string assetsJson, RunOptions options) => await Test(assetsJson, options, null);

        /// <summary>
        /// Trying to build SBOM from provided parameters and validated the result file
        /// </summary>
        /// <param name="assetsJson"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async Task<Bom> Test(string assetsJson, RunOptions options, INugetServiceFactory nugetService)
        {
            nugetService ??= CreateMockNugetServiceFactory();

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/Project.csproj"), new MockFileData(CsprojContents) },
                { MockUnixSupport.Path("c:/ProjectPath/obj/project.assets.json"), new MockFileData(assetsJson) }
            });

            return await Test(options, nugetService, mockFileSystem).ConfigureAwait(false);
        }


        public static async Task<Bom> Test(RunOptions options, MockFileSystem mockFileSystem) => await Test(options, CreateMockNugetServiceFactory(), mockFileSystem);
 

        public static async Task<Bom> Test(RunOptions options, INugetServiceFactory nugetService, MockFileSystem mockFileSystem)
        {
            options.enableGithubLicenses = true;
            options.outputDirectory ??= "/bom/";
            options.outputFilename ??= options.json ? "bom.json" : "bom.xml";
            options.SolutionOrProjectFile ??= MockUnixSupport.Path("c:/ProjectPath/Project.csproj");
            options.disablePackageRestore = true;

            Runner runner = new Runner(mockFileSystem, null, null, null, null, null, null, nugetService);
            int exitCode = await runner.HandleCommandAsync(options);

            Assert.Equal((int)ExitCode.OK, exitCode);

            var expectedFileName = mockFileSystem.Path.Combine(options.outputDirectory, options.outputFilename);



            Assert.True(mockFileSystem.FileExists(MockUnixSupport.Path(expectedFileName)), "Bom file not generated");

            var mockBomFile = mockFileSystem.GetFile(MockUnixSupport.Path(expectedFileName));
            var mockBomFileStream = new MemoryStream(mockBomFile.Contents);
            ValidationResult validationResult;
            if (options.json)
            {
                validationResult = await Json.Validator.ValidateAsync(mockBomFileStream, SpecificationVersion.v1_6).ConfigureAwait(false);
            }
            else
            {
                validationResult = Xml.Validator.Validate(mockBomFileStream, SpecificationVersion.v1_6);
            }
            Assert.True(validationResult.Valid);

            return runner.LastGeneratedBom;
        }

        private const string CsprojContents =
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  " +
            "<PropertyGroup>\n    " +
                "<OutputType>Exe</OutputType>\n    " +
                "<PackageId>SampleProject</PackageId>\n    " +
            "</PropertyGroup>\n\n  " +
            "<ItemGroup>\n    " +          
            "</ItemGroup>\n" +
        "</Project>\n";

        public static void AssertHasDependencyWithChild(Bom bom, string dependencyBomRef, string childBomRef)
            => AssertHasDependencyWithChild(bom, dependencyBomRef, childBomRef, null);
        public static void AssertHasDependencyWithChild(Bom bom, string dependencyBomRef, string childBomRef, string message)
        {
            Assert.True(bom.Dependencies.Any(dep => dep.Ref == dependencyBomRef && dep.Dependencies.Any(child => child.Ref == childBomRef)), message);
        }
    }
}
