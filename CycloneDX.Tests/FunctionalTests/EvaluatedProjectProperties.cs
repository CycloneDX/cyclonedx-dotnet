using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using Moq;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Properties set outside the project file (e.g. in Directory.Build.props) are only visible
    /// through MSBuild evaluation. The project files below intentionally lack them or contain
    /// unresolved property references; the evaluated values come from IProjectEvaluationService.
    /// </summary>
    public class EvaluatedProjectProperties
    {
        private static readonly string AppPath = MockUnixSupport.Path("c:/repo/Foo.Bar.App/Foo.Bar.App.csproj");
        private static readonly string LibPath = MockUnixSupport.Path("c:/repo/Foo.Bar.Lib/Foo.Bar.Lib.csproj");

        private static MockFileSystem GetMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    AppPath,
                    new MockFileData(
                        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>" +
                        "<ItemGroup><ProjectReference Include=\"..\\Foo.Bar.Lib\\Foo.Bar.Lib.csproj\" /></ItemGroup></Project>")
                },
                {
                    LibPath,
                    new MockFileData("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Version>$(LibMajor).0.0</Version></PropertyGroup></Project>")
                },
                {
                    MockUnixSupport.Path("c:/repo/Foo.Bar.Lib/packages.config"),
                    new MockFileData("<?xml version=\"1.0\" encoding=\"utf-8\"?><packages><package id=\"log4net\" version=\"2.0.15\" /></packages>")
                }
            });
        }

        private static Mock<IProjectEvaluationService> GetProjectEvaluationService()
        {
            // keyed by project name, the runner passes normalized full paths
            var evaluatedProperties = new Dictionary<string, Dictionary<string, string>>
            {
                ["Foo.Bar.App"] = new() { ["Version"] = "4.5.6", ["PackageId"] = "Contoso.Foo.Bar.App", ["AssemblyName"] = "Contoso.Foo.Bar.App", ["IsTestProject"] = "" },
                ["Foo.Bar.Lib"] = new() { ["Version"] = "7.0.0", ["PackageId"] = "Contoso.Foo.Bar.Lib", ["AssemblyName"] = "Contoso.Foo.Bar.Lib", ["IsTestProject"] = "" }
            };

            var projectEvaluationService = new Mock<IProjectEvaluationService>();
            projectEvaluationService
                .Setup(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((IEnumerable<string> paths, IEnumerable<string> _, IEnumerable<string> _, string _, string _) => paths.ToDictionary(path => path, path =>
                {
                    var project = new EvaluatedProject();
                    foreach (var property in evaluatedProperties[Path.GetFileNameWithoutExtension(path)])
                    {
                        project.Properties[property.Key] = property.Value;
                    }
                    return new DotnetUtilsResult<EvaluatedProject> { Result = project };
                }));
            return projectEvaluationService;
        }

        [Fact]
        public async Task RootVersionIsEvaluatedByMsbuild()
        {
            var options = new RunOptions { SolutionOrProjectFile = AppPath };

            var bom = await FunctionalTestHelper.Test(options, GetProjectEvaluationService().Object, GetMockFS());

            Assert.Equal("4.5.6", bom.Metadata.Component.Version);
        }

        [Fact]
        public async Task RecursiveProjectReferencesUseEvaluatedPackageIdAndVersion()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                includeProjectReferences = true,
                setNugetPurl = true,
                SolutionOrProjectFile = AppPath
            };

            var bom = await FunctionalTestHelper.Test(options, GetProjectEvaluationService().Object, GetMockFS());

            var lib = Assert.Single(bom.Components, c => c.Type == Component.Classification.Library && c.Name != "log4net");
            Assert.Equal("Contoso.Foo.Bar.Lib", lib.Name);
            Assert.Equal("7.0.0", lib.Version);
            Assert.Equal("pkg:nuget/Contoso.Foo.Bar.Lib@7.0.0", lib.Purl);
            Assert.DoesNotContain(bom.Components, c => c.Version.Contains("$("));
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "pkg:nuget/Foo.Bar.App@4.5.6", "pkg:nuget/Contoso.Foo.Bar.Lib@7.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "pkg:nuget/Contoso.Foo.Bar.Lib@7.0.0", "pkg:nuget/log4net@2.0.15");
        }

        [Fact]
        public async Task EvaluationUsesConfigurationAndFrameworkOptions()
        {
            var options = new RunOptions { SolutionOrProjectFile = AppPath, configuration = "Release", framework = "net8.0" };
            var projectEvaluationService = GetProjectEvaluationService();

            await FunctionalTestHelper.Test(options, projectEvaluationService.Object, GetMockFS());

            projectEvaluationService.Verify(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), "net8.0", "Release"), Times.AtLeastOnce);
            projectEvaluationService.Verify(m => m.EvaluateProjects(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.Is<string>(f => f != "net8.0"), It.IsAny<string>()), Times.Never);
        }
    }
}
