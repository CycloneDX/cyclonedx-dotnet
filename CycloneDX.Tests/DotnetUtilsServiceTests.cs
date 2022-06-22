// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.IO.Abstractions.TestingHelpers;
using CycloneDX.Interfaces;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using Xunit;
using Moq;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX.Tests
{
    public class DotnetUtilsServiceTests
    {
        [Fact]
        public void GetNuGetFallbackFolderPath_ReturnsCorrectFallbackPath()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory
                .CreateDirectory(XFS.Path(@"c:\dotnet\sdk\NuGetFallbackFolder"));

            var dotnetCommandService = new Mock<IDotnetCommandService>();
            dotnetCommandService
                .Setup(m => m.Run("--list-sdks"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"2.2.402 [" + XFS.Path(@"c:\dotnet\sdk") + "]"
                });
            
            var dotnetUtilsService = new DotnetUtilsService(
                fileSystem, dotnetCommandService.Object);

            // act
            var fallbackPath = dotnetUtilsService.GetNuGetFallbackFolderPath();

            Assert.Equal(
                XFS.Path(@"c:\dotnet\sdk\NuGetFallbackFolder"),
                fallbackPath.Result);
        }

        // some SDKs don't have a NugetFallbackFolder
        [Fact]
        public void GetNuGetFallbackFolderPath_ReturnsNullFallbackPath()
        {
            var fileSystem = new MockFileSystem();

            var dotnetCommandService = new Mock<IDotnetCommandService>();
            dotnetCommandService
                .Setup(m => m.Run("--list-sdks"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"2.2.402 [" + XFS.Path(@"c:\dotnet\sdk") + "]"
                });
            
            var dotnetUtilsService = new DotnetUtilsService(
                fileSystem, dotnetCommandService.Object);

            // act
            var fallbackPath = dotnetUtilsService.GetNuGetFallbackFolderPath();

            Assert.True(fallbackPath.Success);
            Assert.Null(fallbackPath.Result);
        }

        [Fact]
        public void GetGlobalPackagesCacheLocation_ReturnsCorrectCachePath()
        {
            var dotnetCommandService = new Mock<IDotnetCommandService>();
            dotnetCommandService
                .Setup(m => m.Run("nuget locals global-packages --list"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"info : global-packages: " + XFS.Path(@"c:\user\.nuget\packages")
                });
            
            var dotnetUtilsService = new DotnetUtilsService(
                new MockFileSystem(), dotnetCommandService.Object);

            // act
            var fallbackPath = dotnetUtilsService.GetGlobalPackagesCacheLocation();

            Assert.Equal(
                XFS.Path(@"c:\user\.nuget\packages"),
                fallbackPath.Result);
        }

        [Fact]
        public void GetPackageCachePaths_ReturnsGlobalCacheAndFallbackCachePaths()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.Directory
                .CreateDirectory(XFS.Path(@"c:\dotnet\sdk\NuGetFallbackFolder"));

            var dotnetCommandService = new Mock<IDotnetCommandService>();
            dotnetCommandService
                .Setup(m => m.Run("--list-sdks"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"2.2.402 [" + XFS.Path(@"c:\dotnet\sdk") + "]"
                });
            dotnetCommandService
                .Setup(m => m.Run("nuget locals global-packages --list"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"info : global-packages: " + XFS.Path(@"c:\user\.nuget\packages")
                });
            
            var dotnetUtilsService = new DotnetUtilsService(
                fileSystem, dotnetCommandService.Object);

            // act
            var cachePaths = dotnetUtilsService.GetPackageCachePaths();

            Assert.Collection(
                cachePaths.Result,
                path => Assert.Equal(XFS.Path(@"c:\user\.nuget\packages"), path),
                path => Assert.Equal(XFS.Path(@"c:\dotnet\sdk\NuGetFallbackFolder"), path));
        }

        [Fact]
        public void GetPackageCachePaths_ReturnsGlobalCacheWithoutFallbackCachePath()
        {
            var fileSystem = new MockFileSystem();

            var dotnetCommandService = new Mock<IDotnetCommandService>();
            dotnetCommandService
                .Setup(m => m.Run("--list-sdks"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"2.2.402 [" + XFS.Path(@"c:\dotnet\sdk") + "]"
                });
            dotnetCommandService
                .Setup(m => m.Run("nuget locals global-packages --list"))
                .Returns(new DotnetCommandResult
                {
                    ExitCode = 0,
                    StdOut = @"info : global-packages: " + XFS.Path(@"c:\user\.nuget\packages")
                });
            
            var dotnetUtilsService = new DotnetUtilsService(
                fileSystem, dotnetCommandService.Object);

            // act
            var cachePaths = dotnetUtilsService.GetPackageCachePaths();

            Assert.Collection(
                cachePaths.Result,
                path => Assert.Equal(XFS.Path(@"c:\user\.nuget\packages"), path));
        }
    }
}
