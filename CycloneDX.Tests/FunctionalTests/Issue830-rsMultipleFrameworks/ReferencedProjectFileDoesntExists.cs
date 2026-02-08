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
    public class Issue830
    {
        /// <summary>
        /// This case is generating a flawed sbom.
        /// CycloneDX can not automatically apply the target framework of
        /// project1 to project2 and thus aggregating the netFramework and
        /// netCore dependencies of project2 
        /// </summary>

        readonly string testFileFolder = "Issue830-rsMultipleFrameworks";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/ProjectPath/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests",testFileFolder, "project1assets.json")))
                },{
                    MockUnixSupport.Path("c:/project2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests",testFileFolder, "project2assets.json")))
                }
            }); 
        }

        [Fact]
        public async Task NoExceptionHappens()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true
            };

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());
        }
    }
}
