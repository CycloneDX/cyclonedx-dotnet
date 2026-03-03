using System.IO;
using System.Linq;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class TransitiveDevDependencyTests
    {
        /// <summary>
        /// A package that is only reachable as a transitive dependency of a dev
        /// dependency (and not from any runtime path) must receive scope=excluded.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task TransitiveDependency_ReachableOnlyViaDevDep_IsExcluded()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_TransitiveOnlyViaDev.json"));
            var options = new RunOptions();

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // SonarAnalyzer.CSharp is the direct dev dep — must be excluded.
            var devDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0);
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);

            // Google.Protobuf is only reachable through the dev dep — must also be excluded.
            var transitiveDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.NotNull(transitiveDep);
            Assert.Equal(Component.ComponentScope.Excluded, transitiveDep.Scope);
        }

        /// <summary>
        /// A package that is reachable from both a dev dependency and a runtime
        /// dependency must keep scope=required (the runtime path wins).
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task TransitiveDependency_ReachableViaDevDepAndRuntimeDep_IsRequired()
        {
            var assetsJson = File.ReadAllText(
                Path.Combine("FunctionalTests", "TestcaseFiles", "DevDependencies_SharedTransitive.json"));
            var options = new RunOptions();

            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            // SonarAnalyzer.CSharp is the dev dep — must be excluded.
            var devDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "SonarAnalyzer.CSharp", true) == 0);
            Assert.NotNull(devDep);
            Assert.Equal(Component.ComponentScope.Excluded, devDep.Scope);

            // Newtonsoft.Json is a direct runtime dep — must be required.
            var runtimeDep = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Newtonsoft.Json", true) == 0);
            Assert.NotNull(runtimeDep);
            Assert.Equal(Component.ComponentScope.Required, runtimeDep.Scope);

            // Google.Protobuf is reachable via both the dev dep AND Newtonsoft.Json —
            // the runtime path wins, so it must be required.
            var sharedTransitive = bom.Components.FirstOrDefault(
                c => string.Compare(c.Name, "Google.Protobuf", true) == 0);
            Assert.NotNull(sharedTransitive);
            Assert.Equal(Component.ComponentScope.Required, sharedTransitive.Scope);
        }
    }
}
