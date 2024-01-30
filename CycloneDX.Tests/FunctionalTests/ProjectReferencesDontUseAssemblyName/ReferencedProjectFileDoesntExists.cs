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
    public class ProjectReferencesDontUseAssemblyName
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/project1/project1.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ProjectReferencesDontUseAssemblyName", "project1csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project2/project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ProjectReferencesDontUseAssemblyName", "project2csproj.xml")))
                },{
                    MockUnixSupport.Path("c:/project1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ProjectReferencesDontUseAssemblyName", "project1assets.json")))
                },{
                    MockUnixSupport.Path("c:/project2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "ProjectReferencesDontUseAssemblyName", "project2assets.json")))
                }
            });
        }

        [Fact]
        public async Task CorrectComponentNameIsReadFromAssetsFile()
        {
            var options = new RunOptions
            {
                //scanProjectReferences = true
                includeProjectReferences = true,
                SolutionOrProjectFile = "c:/project1/project1.csproj"
            };


            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());


            Assert.True(bom.Components.Count == 1, $"Unexpected number of components. Expected 1, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "AssemblyName", true) == 0);
        }

        [Fact]
        public async Task CorrectComponentNameIsReadFromAssetsFileWhenScanProjectReferencesIsTrue()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                includeProjectReferences = true,
                SolutionOrProjectFile = "c:/project1/project1.csproj"
            };


            //Just test that there is no exception
            var bom = await FunctionalTestHelper.Test(options, getMockFS());


            Assert.True(bom.Components.Count == 1, $"Unexpected number of components. Expected 1, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "AssemblyName", true) == 0);
        }

    }
}
