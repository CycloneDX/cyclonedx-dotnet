using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;


namespace CycloneDX.Tests.FunctionalTests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinition
    {
    }
    [Collection("Non-Parallel")]
    public class RelaxedJsonEscaping
    {
        readonly string testFileFolder = "RelaxedJsonEscaping";
        readonly string payload = "Jsön Escaping<script></script> Tëst";


        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/project1/project1.csproj"),
                    new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", testFileFolder, "project1csproj.xml")))
                }
            });
        }

        [Fact(Timeout = 15000)]
        public async Task OutputFormat_UnsafeJson()
        {
            var mockFS = getMockFS();
            var options = new RunOptions
            {
                setName = payload,
                outputFormat = OutputFileFormat.UnsafeJson,
                outputFilename = "bom.json",
                outputDirectory = MockUnixSupport.Path("c:/project1/"),
                SolutionOrProjectFile = MockUnixSupport.Path("c:/project1/project1.csproj")
            };

            var bom = await FunctionalTestHelper.Test(options, mockFS);
            string json = mockFS.File.ReadAllText(MockUnixSupport.Path("c:/project1/bom.json"));
            Assert.Contains(payload, json); // unescaped payload should be visible
        }

        [Fact(Timeout = 15000)]
        public async Task OutputFormat_Json()
        {
            var mockFS = getMockFS();
            var options = new RunOptions
            {
                setName = payload,
                outputFormat = OutputFileFormat.Json,
                outputFilename = "bom.json",
                outputDirectory = MockUnixSupport.Path("c:/project1/"),
                SolutionOrProjectFile = MockUnixSupport.Path("c:/project1/project1.csproj")
            };

            var bom = await FunctionalTestHelper.Test(options, mockFS);
            string json = mockFS.File.ReadAllText(MockUnixSupport.Path("c:/project1/bom.json"));
            Assert.DoesNotContain(payload, json); // special chars should be escaped
        }


        public static IEnumerable<object[]> FormatResolutionTestData =>
            new List<object[]>
            {
        // Format, filename, legacyFlag, expectedFormat, expectedOutputFilename
        new object[] { OutputFileFormat.Auto, null, false, OutputFileFormat.Xml, "bom.xml" },
        new object[] { OutputFileFormat.Auto, null, true, OutputFileFormat.Json, "bom.json" },
        new object[] { OutputFileFormat.Auto, "bom.json", false, OutputFileFormat.Json, "bom.json" },
        new object[] { OutputFileFormat.Auto, "bom.xml", false, OutputFileFormat.Xml, "bom.xml" },
        new object[] { OutputFileFormat.Auto, "strange.output", false, OutputFileFormat.Xml, "strange.output" },
        new object[] { OutputFileFormat.Xml, "strange.output", false, OutputFileFormat.Xml, "strange.output" },
        new object[] { OutputFileFormat.Json, "strange.output", false, OutputFileFormat.Json, "strange.output" },
        new object[] { OutputFileFormat.Json, null, false, OutputFileFormat.Json, "bom.json" },
        new object[] { OutputFileFormat.Xml, null, false, OutputFileFormat.Xml, "bom.xml" },
        new object[] { OutputFileFormat.Xml, null, true, OutputFileFormat.Xml, "bom.xml" },
        new object[] { OutputFileFormat.UnsafeJson, null, false, OutputFileFormat.UnsafeJson, "bom.json" },        
            };

        [Theory(Timeout = 15000)]
        [MemberData(nameof(FormatResolutionTestData))]
        public async Task FormatAndFilenameResolution(
            OutputFileFormat inputFormat,
            string inputFilename,
            bool legacyJsonFlag,
            OutputFileFormat expectedFormat,
            string expectedOutputFilename)
        {
            var mockFS = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {
                    MockUnixSupport.Path("c:/project1/project1.csproj"),
                    new MockFileData(File.ReadAllText(Path.Combine("FunctionalTests", "RelaxedJsonEscaping", "project1csproj.xml")))
                }
            });

            var options = new RunOptions
            {
                setName = "FunctionalFormatTest",
                outputFormat = inputFormat,
                outputFilename = inputFilename,
                outputDirectory = MockUnixSupport.Path("c:/project1/"),
                SolutionOrProjectFile = MockUnixSupport.Path("c:/project1/project1.csproj"),
                json = legacyJsonFlag
            };

            var bom = await FunctionalTestHelper.Test(options, mockFS);

            string expectedPath = MockUnixSupport.Path($"c:/project1/{expectedOutputFilename}");
            Assert.True(mockFS.File.Exists(expectedPath), $"Expected file {expectedPath} to exist");

            string content = mockFS.File.ReadAllText(expectedPath);
            if (expectedFormat == OutputFileFormat.Json || expectedFormat == OutputFileFormat.UnsafeJson)
            {
                Assert.StartsWith("{", content.Trim());
            }
            else if (expectedFormat == OutputFileFormat.Xml)
            {
                Assert.StartsWith("<?xml", content.Trim());
            }
        }


    }
}
