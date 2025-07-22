using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class IprTwoLevelTree
    {
        readonly string testFileFolder = "IPR-TwoLevelTree";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/project1/project1.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2csproj.xml")))
                },{
                  MockUnixSupport.Path("c:/project3/project3.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project3csproj.xml")))                
                },{
                  MockUnixSupport.Path("c:/project3/packages.config"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project3packages.xml")))
                }
            }); 
        }


        [Fact]
        public async Task BaseLineWithoutIPR()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/project1/project1.csproj")

            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project1@0.0.0", "pkg:nuget/log4net@2.0.15");
        }

        [Fact]
        public async Task DependencyGraph()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                includeProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/project1/project1.csproj")

            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project1@0.0.0", "project2@1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project2@1.0.0", "project3@1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project3@1.0.0", "pkg:nuget/log4net@2.0.15");
        }
    }
}
