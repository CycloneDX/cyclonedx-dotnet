// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;

namespace CycloneDX.Tests.FunctionalTests
{
    public class ProjectVersionMetadataTest
    {
        [Fact]
        public async Task BomMetadataVersion_ShouldMatchProjectVersion()
        {
            var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                        "  <PropertyGroup>\n" +
                        "    <OutputType>Exe</OutputType>\n" +
                        "    <Version>2.1.3</Version>\n" +
                        "  </PropertyGroup>\n" +
                        "</Project>\n";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path("c:/ProjectPath/Project.csproj"), new MockFileData(csproj) },
                { XFS.Path("c:/ProjectPath/obj/project.assets.json"), new MockFileData("{}") }
            });

            var options = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path("c:/ProjectPath/Project.csproj"),
                outputDirectory = XFS.Path("c:/ProjectPath/"),
                disablePackageRestore = true
            };

            var bom = await FunctionalTestHelper.Test(options, mockFileSystem);
            Assert.Equal("2.1.3", bom.Metadata.Component.Version);
        }

        [Fact]
        public async Task BomMetadataVersion_SetVersionFlagOverridesProjectVersion()
        {
            var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                        "  <PropertyGroup>\n" +
                        "    <OutputType>Exe</OutputType>\n" +
                        "    <Version>2.1.3</Version>\n" +
                        "  </PropertyGroup>\n" +
                        "</Project>\n";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path("c:/ProjectPath/Project.csproj"), new MockFileData(csproj) },
                { XFS.Path("c:/ProjectPath/obj/project.assets.json"), new MockFileData("{}") }
            });

            var options = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path("c:/ProjectPath/Project.csproj"),
                outputDirectory = XFS.Path("c:/ProjectPath/"),
                disablePackageRestore = true,
                setVersion = "9.9.9"
            };

            var bom = await FunctionalTestHelper.Test(options, mockFileSystem);
            Assert.Equal("9.9.9", bom.Metadata.Component.Version);
        }

        [Fact]
        public async Task BomMetadataVersion_DefaultsTo000WhenNoVersionInProjectFile()
        {
            var csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                        "  <PropertyGroup>\n" +
                        "    <OutputType>Exe</OutputType>\n" +
                        "  </PropertyGroup>\n" +
                        "</Project>\n";

            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path("c:/ProjectPath/Project.csproj"), new MockFileData(csproj) },
                { XFS.Path("c:/ProjectPath/obj/project.assets.json"), new MockFileData("{}") }
            });

            var options = new RunOptions
            {
                SolutionOrProjectFile = XFS.Path("c:/ProjectPath/Project.csproj"),
                outputDirectory = XFS.Path("c:/ProjectPath/"),
                disablePackageRestore = true
            };

            var bom = await FunctionalTestHelper.Test(options, mockFileSystem);
            Assert.Equal("0.0.0", bom.Metadata.Component.Version);
        }
    }
}
