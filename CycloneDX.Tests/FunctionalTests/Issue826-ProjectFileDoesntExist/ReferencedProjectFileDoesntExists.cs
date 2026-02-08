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
    public class Issue826ReferencedProjectFileDoesntExists
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/sln.sln"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "Issue826-ProjectFileDoesntExist", "solution1sln.text")))
                },{
                    MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "Issue826-ProjectFileDoesntExist", "project1csproj.xml"))) }            
            });
        }

        [Fact]
        public async Task NoExceptionHappens()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/ProjectPath/sln.sln")
            };            

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());
        }
    }
}
