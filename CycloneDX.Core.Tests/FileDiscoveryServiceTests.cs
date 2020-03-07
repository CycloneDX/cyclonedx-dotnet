// This file is part of the CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Copyright (c) Steve Springett. All Rights Reserved.

using System.Collections.Generic;
using Xunit;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class FileDiscoveryServiceTests
    {
        [Fact]
        public void GetPackagesConfigFiles_ReturnsPackagesConfigFile_InSpecifiedPath()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\packages.config"), new MockFileData("")},
                });
            var fileDiscoveryService = new FileDiscoveryService(mockFileSystem);

            var files = fileDiscoveryService.GetPackagesConfigFiles(XFS.Path(@"c:\Project"));

            Assert.Collection(files,
                item => Assert.Equal(XFS.Path(@"c:\Project\packages.config"), item));
        }

        [Fact]
        public void GetPackagesConfigFiles_DoesNotReturnNonPackagesConfigFiles_InSpecifiedPath()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\something.config"), new MockFileData("")},
                });
            var fileDiscoveryService = new FileDiscoveryService(mockFileSystem);

            var files = fileDiscoveryService.GetPackagesConfigFiles(XFS.Path(@"c:\Project"));
            
            Assert.Empty(files);
        }

        [Fact]
        public void GetPackagesConfigFiles_ReturnsPackagesConfigFiles_InSpecifiedPathSubdirectories()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\Subdirectory\packages.config"), new MockFileData("")},
                });
            var fileDiscoveryService = new FileDiscoveryService(mockFileSystem);

            var files = fileDiscoveryService.GetPackagesConfigFiles(XFS.Path(@"c:\Project"));
            
            Assert.Collection(files,
                item => Assert.Equal(XFS.Path(@"c:\Project\Subdirectory\packages.config"), item));
        }

        [Fact]
        public void GetPackagesConfigFiles_ReturnsPackagesConfigFiles_InSpecifiedPathAndSubdirectories()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { XFS.Path(@"c:\Project\packages.config"), new MockFileData("")},
                    { XFS.Path(@"c:\Project\Subdirectory\packages.config"), new MockFileData("")},
                });
            var fileDiscoveryService = new FileDiscoveryService(mockFileSystem);

            var files = fileDiscoveryService.GetPackagesConfigFiles(XFS.Path(@"c:\Project"));
            var sortedFiles = new List<string>(files);
            sortedFiles.Sort();
            
            Assert.Collection(files,
                item => Assert.Equal(XFS.Path(@"c:\Project\packages.config"), item),
                item => Assert.Equal(XFS.Path(@"c:\Project\Subdirectory\packages.config"), item));
        }
    }
}
