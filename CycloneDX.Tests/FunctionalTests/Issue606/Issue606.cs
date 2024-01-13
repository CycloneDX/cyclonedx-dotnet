using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class Issue606
    {

        readonly string testFileFolder = "Issue606";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/projectPath/project.sln"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project1/project1.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1assets.json")))                 
                   },{
                    MockUnixSupport.Path("c:/project2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2assets.json")))
                }
            }); 
        }

        [Fact]
        public async Task NoExceptionHappens()
        {
            // One day we might decide that we actually want an exception in this case

            var options = new RunOptions
            {
                scanProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/ProjectPath/Project.sln"),
            };

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());
        }
    }
}
