using System;
using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class SimpleNET6_0Library
    {
        // E2E counterpart: CycloneDX.E2ETests.SimpleProjectTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task TestSimpleNET6_0Library()
        {
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNET6.0Library.json"));            
            var options = new RunOptions
            {
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 1);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "newtonsoft.json", true) == 0 && c.Version == "13.0.3");
        }
    }
}
