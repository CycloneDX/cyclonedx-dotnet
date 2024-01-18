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
                }
            }); 
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

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project1", "project2");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project2", "project3");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "project3", "log4net@2.0.15");
        }
    }
}
