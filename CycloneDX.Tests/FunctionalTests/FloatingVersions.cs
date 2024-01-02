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
    public class FloatingVersions
    {
        [Fact(Timeout = 15000)]
        public async Task TestFloatingVersions()
        {            
            var assetsJson = File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "FloatingVersions.json"));
            var options = new RunOptions
            {
            };


            var bom = await FunctionalTestHelper.Test(assetsJson, options);

            Assert.True(bom.Components.Count == 1);
            Assert.Contains(bom.Components, c => string.Compare(c.Name, "JetBrains.Annotations", true) == 0 && c.Version == "2023.3.0");
        }
    }
}
