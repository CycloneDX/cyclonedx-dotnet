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
    public class SolutionScan_MulitpleVersionsOfOnePackage
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/sln.sln"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "SolutionScan_MulitpleVersionsOfOnePackage", "solution1sln.txt")))
                },{
                    MockUnixSupport.Path("c:/ProjectPath/p1/Project1.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "SolutionScan_MulitpleVersionsOfOnePackage", "project1csproj.xml"))) 
                },{
                    MockUnixSupport.Path("c:/ProjectPath/p2/Project2.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "SolutionScan_MulitpleVersionsOfOnePackage", "project2csproj.xml")))
                        },{
                    MockUnixSupport.Path("c:/ProjectPath/p1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "SolutionScan_MulitpleVersionsOfOnePackage", "p1.project.assets.json")))
                        },{
                    MockUnixSupport.Path("c:/ProjectPath/p2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "SolutionScan_MulitpleVersionsOfOnePackage", "p2.project.assets.json")))
                }
            });
        }

        // E2E counterpart: CycloneDX.E2ETests.SolutionScanTests
        [Fact]
        [Trait("Status", "MigratedToE2E")]
        public async Task GivenASolutionWithTwoIndependentProjectEachIncludingTheSameLibraryInAnotherVersion_WhenCreatingABomOnTheSolution_BothVersionAreInTheSbom()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/ProjectPath/sln.sln")
            };

            //Just test that there is no exception            
            var bom = await FunctionalTestHelper.Test(options, getMockFS());
            FunctionalTestHelper.AssertHasDependency(bom, "pkg:nuget/CycloneDX.Core@8.0.3");
            FunctionalTestHelper.AssertHasDependency(bom, "pkg:nuget/CycloneDX.Core@8.0.2");

        }
    }
}
