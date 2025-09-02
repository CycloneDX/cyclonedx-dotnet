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
    public class Issue847FineGrainedDevDependencies
    {
        readonly string testFileFolder = "Issue847-FineGrainedDependencies/projects/";
        readonly string fineGrainedProject = "c:/project2/project2.csproj";
        readonly string referringFineGrainedProjectProject = "c:/project1/project1.csproj";

        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/project1/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "ReferringFineGrainedDependency", "obj", "project.assets.json")))
                },{
                    MockUnixSupport.Path("c:/project2/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "FineGrainedDependency", "obj", "project.assets.json")))
                },{
                    MockUnixSupport.Path(referringFineGrainedProjectProject),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder,"ReferringFineGrainedDependency", "ReferringFineGrainedDependency.csproj")))
                },{
                    MockUnixSupport.Path(fineGrainedProject),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "FineGrainedDependency", "FineGrainedDependency.csproj")))
                }
            });
        }

        [Fact]
        public async Task DevDependenciesAreIncludedWhenOptionNotSet()
        {
            var options = new RunOptions
            {
                excludeDev = false,
                SolutionOrProjectFile = MockUnixSupport.Path(fineGrainedProject)
            };
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Newtonsoft.Json", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Serilog", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Microsoft.Extensions.Logging", true) == 0);
        }

        [Fact]
        public async Task DevDependenciesAreExcluded()
        {
            var options = new RunOptions
            {
                excludeDev = true,
                SolutionOrProjectFile = MockUnixSupport.Path(fineGrainedProject)
            };
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Newtonsoft.Json", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Serilog", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Microsoft.Extensions.Logging", true) == 0);
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "Microsoft.Extensions.Configuration", true) == 0);
        }

        [Fact]
        public async Task TransitiveDevDependenciesAreExcluded()
        {
            var options = new RunOptions
            {
                excludeDev = true,
                SolutionOrProjectFile = MockUnixSupport.Path(referringFineGrainedProjectProject)
            };
            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Newtonsoft.Json", true) == 0);
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "Serilog", true) == 0);
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "Microsoft.Extensions.Logging", true) == 0);
        }
    }
}
