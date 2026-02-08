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
    public class Issue894
    {
        readonly string testFileFolder = "Issue894";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/projectB/projectB.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/ProjectPath/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests",testFileFolder, "project1assets.json")))
                }
            }); 
        }

        [Fact]
        public async Task NoExceptionHappens()
        {
            var options = new RunOptions
            {
                scanProjectReferences = false
            };

            //Just test that there is no exception
            await FunctionalTestHelper.Test(options, getMockFS());
        }
    }
}
