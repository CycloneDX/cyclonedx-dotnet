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
        [Fact]
        public async Task TestSimpleNETStandardLibrary()
        {
            var assetsJson = File.ReadAllText("FunctionalTests\\TestcaseFiles\\SimpleNETStandardLibrary.json");
            var options = new RunOptions
            {
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);


            Assert.Contains(bom.Components, c => c.Name == "newtonsoft.json" && c.Version == "13.0.3");
        }
    }
}
