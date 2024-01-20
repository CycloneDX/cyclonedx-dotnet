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
    public class ExcludeTestDependencies
    {

        readonly string testFileFolder = "ExcludeTestDependencies";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {

                {
                    MockUnixSupport.Path("c:/solution.sln"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "sln.txt")))
                },{
                    MockUnixSupport.Path("c:/project1/project1.vbproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1.xml")))
                },{
                    MockUnixSupport.Path("c:/testProject1/testProject1.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "testproject1.xml")))
                },{
                    MockUnixSupport.Path("c:/project1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "assetsProject1.json")))
                },{
                    MockUnixSupport.Path("c:/testProject1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "assetsTestProject1.json")))
                }
            });
        }    

        [Fact]
        public async Task IncludesTestDependenciesByDefault()
        {
            var options = new RunOptions
            {
                SolutionOrProjectFile = "c:/solution.sln"
            };
            
            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0 && c.Version == "2.0.15");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "MSTest.TestFramework", true) == 0 && c.Version == "2.2.10");

            Assert.True(bom.Components.First(c => string.Compare(c.Name, "MSTest.TestFramework", true) == 0 && c.Version == "2.2.10").Scope == Component.ComponentScope.Excluded);
        }


        [Fact]
        public async Task ExcludesTestDependenciesWhenOptionIsSet()
        {
            var options = new RunOptions
            {
                SolutionOrProjectFile = "c:/solution.sln",
                excludeTestProjects = true
                
            };

            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0 && c.Version == "2.0.15");
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "MSTest.TestFramework", true) == 0 && c.Version == "2.2.10");
            
        }
    }
}
