using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;


namespace CycloneDX.Tests.FunctionalTests
{
    public class SimpleNETStandardLibrary
    {
        // E2E counterpart: CycloneDX.E2ETests.SimpleProjectTests
        [Fact(Timeout = 15000)]
        [Trait("Status", "MigratedToE2E")]
        public async Task TestSimpleNETStandardLibrary()
        {            
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNETStandardLibrary.json"));
            var options = new RunOptions
            {
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 1);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "newtonsoft.json", true) == 0 && c.Version == "13.0.3");
        }
    }
}
