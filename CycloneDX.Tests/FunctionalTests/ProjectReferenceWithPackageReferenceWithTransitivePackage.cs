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
    public class ProjectReferenceWithPackageReferenceWithTransitivePackage
    {
        readonly string assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "ProjectReferenceWithPackageReferenceWithTransitivePackage.json"));
        readonly RunOptions options = new RunOptions
        {};

        // E2E counterpart: CycloneDX.E2ETests.ProjectReferencesTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task ProjectReferenceWithPackageReferenceWithTransitivePackage_BaseCase()
        {
            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 3, "Unexpected amount of components");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Castle.Core", true) == 0 && c.Version == "5.1.1");
        }

        // E2E counterpart: CycloneDX.E2ETests.ProjectReferencesTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task ProjectReferenceWithPackageReferenceWithTransitivePackage_includeProjectReferences()
        {
            options.includeProjectReferences = true;

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 4, "Unexpected amount of components");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "ClassLibrary1", true) == 0 && c.Version == "1.0.0");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Castle.Core", true) == 0 && c.Version == "5.1.1");

            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "ClassLibrary1@1.0.0", "pkg:nuget/Moq@4.20.70", "expected dependency not found");
        }

        [Fact(Timeout = 15000)]
        public async Task ProjectReferenceWithPackageReferenceWithTransitivePackage_includeProjectReferences_HasNoPurlByDefault()
        {
            options.includeProjectReferences = true;

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            var classLibrary = Assert.Single(bom.Components, c => c.Name == "ClassLibrary1");
            Assert.Null(classLibrary.Purl);
            Assert.Equal("ClassLibrary1@1.0.0", classLibrary.BomRef);
        }

        [Fact(Timeout = 15000)]
        public async Task ProjectReferenceWithPackageReferenceWithTransitivePackage_includeProjectReferences_SetNugetPurl()
        {
            options.includeProjectReferences = true;
            options.setNugetPurl = true;

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            var classLibrary = Assert.Single(bom.Components, c => c.Name == "ClassLibrary1");
            Assert.Equal("pkg:nuget/ClassLibrary1@1.0.0", classLibrary.Purl);
            Assert.Equal("pkg:nuget/ClassLibrary1@1.0.0", classLibrary.BomRef);
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, bom.Metadata.Component.BomRef, "pkg:nuget/ClassLibrary1@1.0.0", "expected dependency on project reference not found");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "pkg:nuget/ClassLibrary1@1.0.0", "pkg:nuget/Moq@4.20.70", "expected dependency of project reference not found");
        }
    }
}
