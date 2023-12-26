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

using System.Collections.Generic;
using System.Net.Http;
using System.IO.Abstractions.TestingHelpers;
using RichardSzalay.MockHttp;
using CycloneDX.Models;
using System;
using System.Text;

namespace CycloneDX.Tests
{
    static class Helpers
    {
        static string NugetResponse(DotnetDependency package)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
                <metadata>
                    <id>" + package.Name + @"</id>
                    <version>" + package.Version + @"</version>
                </metadata>
                </package>";
        }

        static void AddNugetResponse(this MockHttpMessageHandler mockHttp, DotnetDependency package)
        {
            mockHttp.When("https://api.nuget.org/v3-flatcontainer/" + package.Name + "/" + package.Version + "/" + package.Name + ".nuspec")
                .Respond("application/xml", NugetResponse(package));
        }

        public static HttpClient GetNugetMockHttpClient(IEnumerable<DotnetDependency> packages)
        {
            var mockHttp = new MockHttpMessageHandler();
            foreach (var package in packages)
            {
                mockHttp.AddNugetResponse(package);
            }
            var client = mockHttp.ToHttpClient();
            return client;
        }

        public static MockFileData GetProjectFileWithReferences(IEnumerable<string> projects, IEnumerable<DotnetDependency> packages)
        {
            var stringBuilder = new StringBuilder();
           stringBuilder.Append("<Project>");

            if (projects != null)
            {
                foreach (var project in projects)
                {
                    stringBuilder.Append(@"<ProjectReference Include=""" + project + @""" />");
                }
            }

            if (packages != null)
            {
                foreach (var package in packages)
                {
                    stringBuilder.Append(@"<PackageReference Include=""" + package.Name + @""" Version=""" + package.Version + @""" />");
                }
            }

            stringBuilder.Append("</Project>");
            var fileData = stringBuilder.ToString();
            return new MockFileData(fileData);
        }

        public static MockFileData GetProjectFileWithPackageReferences(IEnumerable<DotnetDependency> packages)
        {
            return GetProjectFileWithReferences(null, packages);
        }

        public static MockFileData GetProjectFileWithPackageReference(string packageName, string packageVersion)
        {
            return GetProjectFileWithPackageReferences(new List<DotnetDependency> { new DotnetDependency { Name = packageName, Version = packageVersion } });
        }

        public static MockFileData GetPackagesFileWithPackageReferences(IEnumerable<DotnetDependency> packages)
        {
            var fileData = "<packages>";
            foreach (var package in packages)
            {
                fileData += @"<package id=""" + package.Name + @""" version=""" + package.Version + @""" />";
            }
            fileData += "</packages>";
            return new MockFileData(fileData);

        }

        public static DotnetCommandResult GetDotnetListPackagesResult(IEnumerable<(string projectName, (string packageName, string version)[] packages)> projects)
        {
            StringBuilder stdout = new StringBuilder();
            foreach (var project in projects)
            {
                stdout.AppendLine(string.Join(Environment.NewLine, new[] { $"Project '{project.projectName}' has the following package references", $"    [netcoreapp3.1]:", $"Top-level Package    Requested    Resolved" }));
                foreach (var package in project.packages)
                {
                    stdout.AppendLine($"    > {package.packageName}    {package.version}    {package.version}    ");
                }
            }
            return new DotnetCommandResult
            {
                ExitCode = 0,
                StdOut = stdout.ToString()
            };
        }

        public static MockFileData GetPackagesFileWithPackageReference(string packageName, string packageVersion)
        {
            return GetPackagesFileWithPackageReferences(new List<DotnetDependency> { new DotnetDependency { Name = packageName, Version = packageVersion}});
        }

        public static MockFileData GetProjectFileWithProjectReferences(IEnumerable<string> projects)
        {
            return GetProjectFileWithReferences(projects, null);
        }

        public static MockFileData GetEmptyProjectFile()
        {
            return GetProjectFileWithReferences(null, null);
        }
    }
}
