using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ProjectReferenceWithPackageReferenceWithTransistivePackage
    {
        readonly string assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "ProjectReferenceWithPackageReferenceWithTransistivePackage.json"));
        readonly RunOptions options = new RunOptions
        {};

        [Fact(Timeout = 15000)]
        public async Task ProjectReferenceWithPackageReferenceWithTransistivePackage_BaseCase()
        {
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 3, "Unexpected amount of components");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Castle.Core", true) == 0 && c.Version == "5.1.1");
        }

        [Fact(Timeout = 15000)]
        public async Task ProjectReferenceWithPackageReferenceWithTransistivePackage_Basecase()
        {
            options.includeProjectReferences = true;

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 4, "Unexpected amount of components");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "ClassLibrary1", true) == 0 && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Castle.Core", true) == 0 && c.Version == "5.1.1");

            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "ClassLibrary1@1.0.0", "pkg:nuget/Castle.Core@5.1.1", "expected dependency not found");
        }
    }
}
