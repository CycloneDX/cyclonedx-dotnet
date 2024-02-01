using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ExcludeTransitiveDevDependency
    {  

        [Fact]
        public async Task DevDependenciesNormalyGoIntoTheBom()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "ExcludeTransitiveDevDependency", "AssetsFile.json"));
            var options = new RunOptions
            {
            };



            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Microsoft.CodeAnalysis.FxCopAnalyzers", true) == 0 && c.Version == "3.3.2");
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "Microsoft.CodeQuality.Analyzers", true) == 0 && c.Version == "3.3.2");
            Assert.True(bom.Components.First(c => c.Name == "Microsoft.CodeAnalysis.FxCopAnalyzers").Scope == Component.ComponentScope.Excluded, "Scope of development dependency is not excluded.");
            Assert.True(bom.Components.First(c => c.Name == "Microsoft.CodeQuality.Analyzers").Scope == Component.ComponentScope.Excluded, "Scope of development dependency is not excluded.");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "Project@0.0.0", "pkg:nuget/Microsoft.CodeAnalysis.FxCopAnalyzers@3.3.2", "expected dependency not found");
            FunctionalTestHelper.AssertHasDependencyWithChild(bom, "pkg:nuget/Microsoft.CodeAnalysis.FxCopAnalyzers@3.3.2", "pkg:nuget/Microsoft.CodeQuality.Analyzers@3.3.2", "expected dependency not found");
            



        }

        [Fact]
        public async Task DevDependenciesAreExcludedWithExcludeDevDependencies()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "ExcludeTransitiveDevDependency", "AssetsFile.json"));
            var options = new RunOptions
            {
                excludeDev = true
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 0);
            Assert.True(bom.Dependencies.Count == 1); // only the meta component


        }
    }
}
