using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    /// <summary>
    /// Some dependencies reference different dependency versions based on the project's target framework.
    /// For example, Microsoft.Data.SqlClient@5.2.1 references System.Runtime.Caching@6.0.0 when targeting net6.0
    /// but references System.Runtime.Caching@8.0.0 when targeting net8.0
    ///
    /// This test ensures that the generated SBOM lists both versions as children.
    ///
    /// Given a dependency, D1, which  may
    /// a solution file, S1.
    ///   and S1 contains two project files, P1 and P2
    ///   and P1 targets net6.0
    ///   and P1 references Microsoft.Data.SqlClient@5.2.1
    ///   and P2 targets net8.0
    ///   and P2 references Microsoft.Data.SqlClient@5.2.1
    /// When scanning S1
    /// Then the dependencies of
    /// System.Runtime.Caching@6.0.0 should be a child of Microsoft.Data.SqlClient@5.2.1
    ///   and System.Runtime.Caching@8.0.0 should be a child of Microsoft.Data.SqlClient@5.2.1
    /// </summary>
    public class DepsOnSeveralTargetFrameworks
    {
        private MockFileData Source(string file)
        {
            return new MockFileData(File.ReadAllText(
                Path.Combine("FunctionalTests", "Issue911-depsOnSeveralTargetFrameworks", file)));
        }

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/ProjectPath/sln.sln"), Source("solution1sln.text")
                },{
                    MockUnixSupport.Path("c:/ProjectPath/project1/Project1.csproj"), Source("project1csproj.xml")
                },{
                    MockUnixSupport.Path("c:/ProjectPath/project1/obj/project.assets.json"), Source("project1assets.json")
                },{
                    MockUnixSupport.Path("c:/ProjectPath/project2/Project2.csproj"), Source("project2csproj.xml")
                },{
                    MockUnixSupport.Path("c:/ProjectPath/project2/obj/project.assets.json"), Source("project2assets.json")
                }
            }); 
        }

        [Fact (Skip = "#911 is not yet corrected")]
        public async Task NoExceptionHappens()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true,
                SolutionOrProjectFile = MockUnixSupport.Path("c:/ProjectPath/sln.sln")
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());
            FunctionalTestHelper.AssertHasDependencyWithChild(
                bom,
                "pkg:nuget/Microsoft.Data.SqlClient@5.2.1",
                "pkg:nuget/System.Runtime.Caching@6.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(
                bom,
                "pkg:nuget/Microsoft.Data.SqlClient@5.2.1",
                "pkg:nuget/System.Runtime.Caching@8.0.0");
        }
    }
}
