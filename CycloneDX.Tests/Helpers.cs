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
using System.Net.Http;
using System.IO.Abstractions.TestingHelpers;
using RichardSzalay.MockHttp;
using CycloneDX.Models;

namespace CycloneDX.Tests
{
    static class Helpers
    {
        static string NugetResponse(NugetPackage package)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>" + package.Name + @"</id>
                    <version>" + package.Version + @"</version>
                </metadata>
                </package>";
        }

        static void AddNugetResponse(this MockHttpMessageHandler mockHttp, NugetPackage package)
        {
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/" + package.Name + "/" + package.Version + "/" + package.Name + ".nuspec")
                .Respond("application/xml", NugetResponse(package));
        }

        public static HttpClient GetNugetMockHttpClient(IEnumerable<NugetPackage> packages)
        {
            var mockHttp = new MockHttpMessageHandler();
            foreach (var package in packages)
            {
                mockHttp.AddNugetResponse(package);
            }
            var client = mockHttp.ToHttpClient();
            return client;
        }

        public static MockFileData GetProjectFileWithReferences(IEnumerable<string> projects, IEnumerable<NugetPackage> packages)
        {
            var fileData = "<Project>";

            if (projects != null)
            foreach (var project in projects)
            {
                fileData += @"<ProjectReference Include=""" + project + @""" />";
            }

            if (packages != null)
            foreach (var package in packages)
            {
                fileData += @"<PackageReference Include=""" + package.Name + @""" Version=""" + package.Version + @""" />";
            }

            fileData += "</Project>";
            return new MockFileData(fileData);
        }

        public static MockFileData GetProjectFileWithPackageReferences(IEnumerable<NugetPackage> packages)
        {
            return GetProjectFileWithReferences(null, packages);
        }

        public static MockFileData GetProjectFileWithPackageReference(string packageName, string packageVersion)
        {
            return GetProjectFileWithPackageReferences(new List<NugetPackage> { new NugetPackage { Name = packageName, Version = packageVersion } });
        }

        public static MockFileData GetPackagesFileWithPackageReferences(IEnumerable<NugetPackage> packages)
        {
            var fileData = "<packages>";
            foreach (var package in packages)
            {
                fileData += @"<package id=""" + package.Name + @""" version=""" + package.Version + @""" />";
            }
            fileData += "</packages>";
            return new MockFileData(fileData);

        }

        public static MockFileData GetPackagesFileWithPackageReference(string packageName, string packageVersion)
        {
            return GetPackagesFileWithPackageReferences(new List<NugetPackage> { new NugetPackage { Name = packageName, Version = packageVersion}});
        }

        public static MockFileData GetProjectFileWithProjectReferences(IEnumerable<string> projects)
        {
            return GetProjectFileWithReferences(projects, null);
        }
    }
}
