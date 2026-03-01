using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;
using Moq;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    /// <summary>
    /// Ensures that generated BOM files are valid.
    /// </summary>
    public class ValidationTests
    {
        [Theory]
        //[InlineData("xml", false)]
        [InlineData("xml", true)]
        //[InlineData("json", false)]
        [InlineData("json", true)]
        public async Task Validation(string fileFormat, bool disableGitHubLicenses)
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path(@"c:\ProjectPath\Project.csproj"), new MockFileData(CsprojContents) }
            });

            var packages = new HashSet<DotnetDependency>
            {
                new() { Name = "DotNetEnv", Version = "1.4.0" },
                new() { Name = "HtmlAgilityPack", Version = "1.11.30" },
                new() { Name = "LibGit2Sharp", Version = "0.27.0-preview-0096" },
                new() { Name = "NLog", Version = "4.7.7" },
                new() { Name = "RestSharp", Version = "106.11.7" },
            };

            var mockProjectFileService = new Mock<IProjectFileService>();
            mockProjectFileService.Setup(mock =>
                mock.GetProjectDotnetDependencysAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>())
            ).ReturnsAsync(packages);

            Runner runner = new Runner(fileSystem: mockFileSystem, null, null, null, null, projectFileService: mockProjectFileService.Object, solutionFileService: null, null);
            

            RunOptions runOptions = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path(@"c:\ProjectPath\Project.csproj"),
                outputDirectory = XFS.Path(@"c:\NewDirectory"),
                outputFormat = fileFormat == "json" ? OutputFileFormat.Json : OutputFileFormat.Xml,
                enableGithubLicenses = !disableGitHubLicenses,
            };

            var exitCode = await runner.HandleCommandAsync(runOptions);


            Assert.Equal((int)ExitCode.OK, exitCode);

            var expectedFileName = @"c:\NewDirectory\bom.xml";
            if (fileFormat == "json")
            {
                expectedFileName = @"c:\NewDirectory\bom.json";
            }
            Assert.True(mockFileSystem.FileExists(MockUnixSupport.Path(expectedFileName)));

            var mockBomFile = mockFileSystem.GetFile(MockUnixSupport.Path(expectedFileName));
            var mockBomFileStream = new MemoryStream(mockBomFile.Contents);
            ValidationResult validationResult;
            if (fileFormat == "json")
            {
                validationResult = await Json.Validator.ValidateAsync(mockBomFileStream, SpecificationVersion.v1_7).ConfigureAwait(true);
            }
            else
            {
                validationResult = Xml.Validator.Validate(mockBomFileStream, SpecificationVersion.v1_7);
            }

            Assert.True(validationResult.Valid);
        }

        private const string CsprojContents =
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  " +
                "<PropertyGroup>\n    " +
                    "<OutputType>Exe</OutputType>\n    " +
                    "<PackageId>SampleProject</PackageId>\n    " +
                "</PropertyGroup>\n\n  " +
                "<ItemGroup>\n    " +
                    "<PackageReference Include=\"DotNetEnv\" Version=\"1.4.0\" />\n      " +
                    "<PackageReference Include=\"HtmlAgilityPack\" Version=\"1.11.30\" />\n      " +
                    "<PackageReference Include=\"LibGit2Sharp\" Version=\"0.27.0-preview-0096\" />\n      " +
                    "<PackageReference Include=\"NLog\" Version=\"4.7.7\" />\n      " +
                    "<PackageReference Include=\"RestSharp\" Version=\"106.11.7\" />\n" +
                "</ItemGroup>\n" +
            "</Project>\n";
    }
}
