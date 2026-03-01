using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests
{
    public class MetaData
    {
        private MockFileSystem getMockFS()
        {
            return new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { MockUnixSupport.Path("c:/ProjectPath/obj/project.assets.json"),
                        new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "SimpleNET6.0Library.json"))) },
                { MockUnixSupport.Path("c:/ProjectPath/Project.csproj"), new MockFileData(FunctionalTestHelper.CsprojContents) },
                { MockUnixSupport.Path("c:/ProjectPath/metadata.xml"),
                     new MockFileData(
                            File.ReadAllText(Path.Combine("FunctionalTests", "TestcaseFiles", "metadata.xml"))) }
            });
        }

        [Fact]
        public async Task ImportedMetaDataAreInBomOutput()
        {
            var options = new RunOptions
            {
                importMetadataPath = MockUnixSupport.Path("c:/ProjectPath/metadata.xml")
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Equal("CycloneDX", bom.Metadata.Component.Name);
            Assert.Equal("1.3.0", bom.Metadata.Component.Version);
            Assert.Equal(Component.Classification.Application, bom.Metadata.Component.Type);
            Assert.False(string.IsNullOrEmpty(bom.Metadata.Component.Description));
            Assert.Equal("Apache License 2.0", bom.Metadata.Component.Licenses.First().License.Name);
            Assert.Equal("Apache-2.0", bom.Metadata.Component.Licenses.First().License.Id);
            Assert.Equal("pkg:nuget/CycloneDX@1.3.0", bom.Metadata.Component.Purl);
        }

        [Fact]
        public async Task IfNoMetadataIsImportedTimestampIsSetAutomatically()
        {
            var options = new RunOptions
            {             
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());
            Assert.True(bom.Metadata.Timestamp.Value > DateTime.UtcNow.AddMinutes(-10));
        }

        [Fact]
        public async Task IfMetadataWithoutTimestampIsImportedTimestampStillGetsSet()
        {
            var options = new RunOptions
            {
                importMetadataPath = MockUnixSupport.Path("c:/ProjectPath/metadata.xml")
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());
            Assert.True(bom.Metadata.Timestamp.Value > DateTime.UtcNow.AddMinutes(-10));
        }

        [Fact]
        public async Task SetVersionOverwritesVersionWhenMetadataAreProvided()
        {
            var options = new RunOptions
            {
                importMetadataPath = MockUnixSupport.Path("c:/ProjectPath/metadata.xml"),
                setVersion = "3.0.4"
                
            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Equal("CycloneDX", bom.Metadata.Component.Name);
            Assert.Equal("3.0.4", bom.Metadata.Component.Version);
            Assert.Equal(Component.Classification.Application, bom.Metadata.Component.Type);
            Assert.False(string.IsNullOrEmpty(bom.Metadata.Component.Description));
            Assert.Equal("Apache License 2.0", bom.Metadata.Component.Licenses.First().License.Name);
            Assert.Equal("Apache-2.0", bom.Metadata.Component.Licenses.First().License.Id);
            Assert.Equal("pkg:nuget/CycloneDX@1.3.0", bom.Metadata.Component.Purl);
        }

        [Fact]
        public async Task SetNameOverwritesNameWhenMetadataAreProvided()
        {
            var options = new RunOptions
            {
                importMetadataPath = MockUnixSupport.Path("c:/ProjectPath/metadata.xml"),
                setName = "Foo"

            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Equal("Foo", bom.Metadata.Component.Name);
            Assert.Equal("1.3.0", bom.Metadata.Component.Version);
            Assert.Equal(Component.Classification.Application, bom.Metadata.Component.Type);
            Assert.False(string.IsNullOrEmpty(bom.Metadata.Component.Description));
            Assert.Equal("Apache License 2.0", bom.Metadata.Component.Licenses.First().License.Name);
            Assert.Equal("Apache-2.0", bom.Metadata.Component.Licenses.First().License.Id);
            Assert.Equal("pkg:nuget/CycloneDX@1.3.0", bom.Metadata.Component.Purl);
        }

        [Fact]
        public async Task SetTypeOverwritesTypeWhenMetadataAreProvided()
        {
            var options = new RunOptions
            {
                importMetadataPath = MockUnixSupport.Path("c:/ProjectPath/metadata.xml"),
                setType = Component.Classification.Container

            };

            var bom = await FunctionalTestHelper.Test(options, getMockFS());

            Assert.Equal("CycloneDX", bom.Metadata.Component.Name);
            Assert.Equal("1.3.0", bom.Metadata.Component.Version);
            Assert.Equal(Component.Classification.Container, bom.Metadata.Component.Type);
            Assert.False(string.IsNullOrEmpty(bom.Metadata.Component.Description));
            Assert.Equal("Apache License 2.0", bom.Metadata.Component.Licenses.First().License.Name);
            Assert.Equal("Apache-2.0", bom.Metadata.Component.Licenses.First().License.Id);
            Assert.Equal("pkg:nuget/CycloneDX@1.3.0", bom.Metadata.Component.Purl);
        }
    }
}
