using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public sealed class Issue669
    {
        [Theory]
        [InlineData("testproject.csproj.xml", true)]
        [InlineData("nontestproject.csproj.xml", false)]
        public void IsTestProjectTrueWithImportedPropsTargets(string projectFile, bool expectedIsTestProject)
        {
            var mockDotnetUtilsService = new Mock<IDotnetUtilsService>();
            mockDotnetUtilsService
                .Setup(s => s.Restore(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new DotnetUtilsResult());
            mockDotnetUtilsService
                 .Setup(s => s.GetAssetsPath(It.IsAny<string>()))
                 .Returns(new DotnetUtilsResult<string>() { Result = "" });
            var mockPackageFileService = new Mock<IPackagesFileService>();
            var mockProjectAssetsFileService = new Mock<IProjectAssetsFileService>();

            var projectFileService = new ProjectFileService(
                new FileSystem(),
                mockDotnetUtilsService.Object,
                mockPackageFileService.Object,
                mockProjectAssetsFileService.Object);

            string physicalProjectPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FunctionalTests", "Issue669-IsTestProjectEvaluation", projectFile);

            Assert.Equal(projectFileService.IsTestProject(physicalProjectPath), expectedIsTestProject);
        }
    }
}
