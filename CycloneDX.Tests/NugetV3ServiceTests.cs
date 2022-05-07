using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.Models;
using CycloneDX.Services;
using Moq;
using NuGet.Common;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests
{
    public class NugetV3ServiceTests
    {
        [Fact]
        public void GetCachedNuspecFilename_ReturnsFullPath()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache1\dummypackage\1.2.3\dummypackage.nuspec"), "" },
                { XFS.Path(@"c:\nugetcache2\testpackage\1.2.3\testpackage.nuspec"), "" },
            });
            var cachePaths = new List<string>
            {
                XFS.Path(@"c:\nugetcache1"),
                XFS.Path(@"c:\nugetcache2"),
            };
            var mockGithubService = new Mock<IGithubService>();
            var nugetService = new NugetV3Service(null, mockFileSystem, cachePaths, mockGithubService.Object,
                new NullLogger(), false);

            var nuspecFilename = nugetService.GetCachedNuspecFilename("TestPackage", "1.2.3");

            Assert.Equal(XFS.Path(@"c:\nugetcache2\testpackage\1.2.3\testpackage.nuspec"), nuspecFilename);
        }

        [Fact]
        public async Task GetComponent_FromCachedNuspecFile_ReturnsComponent()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal("testpackage", component.Name);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetHashFile_ReturnsComponentWithHash()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";
            byte[] sampleHash = new byte[] { 1, 2, 3, 4, 5, 6, 78, 125, 200 };

            var nugetHashFileContents = Convert.ToBase64String(sampleHash);
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg.sha512"), new MockFileData(nugetHashFileContents) },
            });
            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(),false);


            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Equal(Hash.HashAlgorithm.SHA_512, component.Hashes[0].Alg);
            Assert.Equal(BitConverter.ToString(sampleHash).Replace("-", string.Empty), component.Hashes[0].Content);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetFile_ReturnsComponentWithHash()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";

            var nugetFileContent = "FooBarBaz";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg"), new MockFileData(nugetFileContent) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            byte[] hashBytes;
            using (SHA512 sha = SHA512.Create())
            {
                hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(nugetFileContent));
            }

            Assert.Equal(Hash.HashAlgorithm.SHA_512, component.Hashes[0].Alg);
            Assert.Equal(BitConverter.ToString(hashBytes).Replace("-", string.Empty), component.Hashes[0].Content);
        }

        [Fact]
        public async Task GetComponent_FromCachedNugetFile_DoNotReturnsHash_WhenDisabled()
        {
            var nuspecFileContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>testpackage</id>
                </metadata>
                </package>";

            var nugetFileContent = "FooBarBaz";
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.nuspec"), new MockFileData(nuspecFileContents) },
                { XFS.Path(@"c:\nugetcache\testpackage\1.0.0\testpackage.1.0.0.nupkg"), new MockFileData(nugetFileContent) },
            });

            var nugetService = new NugetV3Service(null,
                mockFileSystem,
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(),
                true);

            var component = await nugetService.GetComponentAsync("testpackage", "1.0.0", Component.ComponentScope.Required).ConfigureAwait(false);

            Assert.Null(component.Hashes);
        }

        [Fact]
        public async Task GetComponentFromNugetOrgReturnsComponent()
        {

            var nugetService = new NugetV3Service(null,
                new MockFileSystem(),
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(), false);


            var packageName = "Newtonsoft.Json";
            var packageVersion = "13.0.1";
            var component = await nugetService
                .GetComponentAsync("Newtonsoft.Json", packageVersion, Component.ComponentScope.Required)
                .ConfigureAwait(false);

            Assert.Equal(packageName, component.Name);
            Assert.Equal(packageVersion, component.Version);
        }

        [Fact]
        public async Task GetComponentFromNugetOrgReturnsComponent_disableHashComputation_true()
        {
            var nugetService = new NugetV3Service(null,
                new MockFileSystem(),
                new List<string> { XFS.Path(@"c:\nugetcache") },
                new Mock<IGithubService>().Object,
                new NullLogger(),true);

            var packageName = "Newtonsoft.Json";
            var packageVersion = "13.0.1";
            var component = await nugetService
                .GetComponentAsync("Newtonsoft.Json", packageVersion, Component.ComponentScope.Required)
                .ConfigureAwait(false);

            Assert.Equal(packageName, component.Name);
            Assert.Equal(packageVersion, component.Version);
        }
    }
}
