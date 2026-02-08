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
    public class FrameworkPackagesConfigRecursive
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/Project.csproj"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFrameworkPackagesConfigRecursive", "project1csproj.xml"))) },
                { MockUnixSupport.Path("c:/ProjectPath/packages.config"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFrameworkPackagesConfigRecursive", "packages1.config"))) },
                 { MockUnixSupport.Path("c:/ConsoleApp2/ConsoleApp2.csproj"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFrameworkPackagesConfigRecursive", "project2csproj.xml"))) },
                 { MockUnixSupport.Path("c:/ConsoleApp2/packages.config"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFrameworkPackagesConfigRecursive", "packages2.config"))) }
            });
        }

        [Fact]
        public async Task WithoutRecursiveScan()
        {
            var options = new RunOptions
            {             
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 7, $"Unexpected number of components. Expected 7, got {bom.Components.Count}");
            Assert.DoesNotContain(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0 && c.Version == "2.0.15");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "pkg:nuget/Stubble.Core@1.10.8");
            

        }

        [Fact]
        public async Task WithRecursiveScan()
        {
            var options = new RunOptions
            {
                scanProjectReferences = true
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 8, $"Unexpected number of components. Expected 8, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0 && c.Version == "2.0.15");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "pkg:nuget/Stubble.Core@1.10.8");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "pkg:nuget/log4net@2.0.15");

        }

        [Fact]
        public async Task WithRecursiveScanIPR()
        {
            var options = new RunOptions
            {
                includeProjectReferences = true,
                scanProjectReferences = true
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.True(bom.Components.Count == 9, $"Unexpected number of components. Expected 9, got {bom.Components.Count}");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "log4net", true) == 0 && c.Version == "2.0.15");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "ConsoleApp2", true) == 0 && c.Version == "1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "pkg:nuget/Stubble.Core@1.10.8");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "ConsoleApp2@1.0.0");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "ConsoleApp2@1.0.0", "pkg:nuget/log4net@2.0.15");

        }
    }
}
