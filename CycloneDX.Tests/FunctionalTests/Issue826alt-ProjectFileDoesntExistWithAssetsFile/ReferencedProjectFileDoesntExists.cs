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
    public class Issue826ReferencedProjectFileDoesntExistWithAssetsFile
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "Issue826alt-ProjectFileDoesntExistWithAssetsFile", "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "Issue826alt-ProjectFileDoesntExistWithAssetsFile", "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "Issue826alt-ProjectFileDoesntExistWithAssetsFile", "project2assets.json"))) 
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

        [Fact]
        public async Task NoExceptionHappensWithIPR()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                includeProjectReferences = true                
            };            

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());
        }
    }
}
