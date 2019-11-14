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

namespace CycloneDX.IntegrationTests
{
    public class Tests
    {
        [Fact]
        public void CallingCycloneDX_WithNoPackages_GeneratesEmptyBom()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Solution\Solution.sln"), new MockFileData(@"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project"", ""Project\Project.csproj"", ""{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}""
EndProject
Global
GlobalSection(SolutionConfigurationPlatforms) = preSolution
Debug|Any CPU = Debug|Any CPU
Debug|x64 = Debug|x64
Debug|x86 = Debug|x86
Release|Any CPU = Release|Any CPU
Release|x64 = Release|x64
Release|x86 = Release|x86
EndGlobalSection
GlobalSection(SolutionProperties) = preSolution
HideSolutionNode = FALSE
EndGlobalSection
GlobalSection(ProjectConfigurationPlatforms) = postSolution
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|Any CPU.Build.0 = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|x64.ActiveCfg = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|x64.Build.0 = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|x86.ActiveCfg = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Debug|x86.Build.0 = Debug|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|Any CPU.ActiveCfg = Release|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|Any CPU.Build.0 = Release|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|x64.ActiveCfg = Release|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|x64.Build.0 = Release|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|x86.ActiveCfg = Release|Any CPU
{318AD44C-F5E0-49E4-B474-AE5D20C74A1A}.Release|x86.Build.0 = Release|Any CPU
EndGlobalSection
EndGlobal")},
                { 
                    XFS.Path(@"c:\Solution\Project\Project.csproj"), 
                    new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk""><ItemGroup></ItemGroup></Project>")
                },
            });
            Program.fileSystem = mockFileSystem;

            var exitCode = Program.Main(new string[] {
                XFS.Path(@"c:\Solution\Solution.sln"),
                "--noSerialNumber",
                "--out", XFS.Path(@"c:\Solution")
            });
            var bomContents = mockFileSystem.File.ReadAllText(XFS.Path(@"c:\Solution\bom.xml"));

            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<bom version=""1"" xmlns=""http://cyclonedx.org/schema/bom/1.1"">
  <components />
</bom>", bomContents);
        }

        [Fact]
        public void CallingCycloneDX_WithNoProjects_GeneratesEmptyBom()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Solution\Solution.sln"), new MockFileData(@"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Debug|x64 = Debug|x64
    Debug|x86 = Debug|x86
    Release|Any CPU = Release|Any CPU
    Release|x64 = Release|x64
    Release|x86 = Release|x86
  EndGlobalSection
  GlobalSection(SolutionProperties) = preSolution
    HideSolutionNode = FALSE
  EndGlobalSection
EndGlobal")}
            });
            Program.fileSystem = mockFileSystem;

            var exitCode = Program.Main(new string[] {
                XFS.Path(@"c:\Solution\Solution.sln"),
                "--noSerialNumber",
                "--out", XFS.Path(@"c:\Solution")
            });
            var bomContents = mockFileSystem.File.ReadAllText(XFS.Path(@"c:\Solution\bom.xml"));

            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<bom version=""1"" xmlns=""http://cyclonedx.org/schema/bom/1.1"">
  <components />
</bom>", bomContents);
        }

        [Fact]
        public void CallingCycloneDX__GeneratesBom()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { XFS.Path(@"c:\Solution\Solution.sln"), new MockFileData(@"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26124.0
MinimumVisualStudioVersion = 15.0.26124.0
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project.WithPackages"", ""Project.WithPackages\Project.WithPackages.csproj"", ""{5849EBB9-CC38-4FCE-A3E1-3501C674249C}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Project.NoPackages"", ""Project.NoPackages\Project.NoPackages.csproj"", ""{C6071BA2-1168-41E7-9BD3-A48585C9637A}""
EndProject
Project(""{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"") = ""Project.Vb"", ""Project.Vb\Project.Vb.vbproj"", ""{A775F37D-4AA6-47D8-8305-6DFDB843B347}""
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Debug|x64 = Debug|x64
    Debug|x86 = Debug|x86
    Release|Any CPU = Release|Any CPU
    Release|x64 = Release|x64
    Release|x86 = Release|x86
  EndGlobalSection
  GlobalSection(SolutionProperties) = preSolution
    HideSolutionNode = FALSE
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|x64.ActiveCfg = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|x64.Build.0 = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|x86.ActiveCfg = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Debug|x86.Build.0 = Debug|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|Any CPU.ActiveCfg = Release|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|Any CPU.Build.0 = Release|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|x64.ActiveCfg = Release|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|x64.Build.0 = Release|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|x86.ActiveCfg = Release|Any CPU
    {5849EBB9-CC38-4FCE-A3E1-3501C674249C}.Release|x86.Build.0 = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|x64.ActiveCfg = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|x64.Build.0 = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|x86.ActiveCfg = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Debug|x86.Build.0 = Debug|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|Any CPU.ActiveCfg = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|Any CPU.Build.0 = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|x64.ActiveCfg = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|x64.Build.0 = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|x86.ActiveCfg = Release|Any CPU
    {C6071BA2-1168-41E7-9BD3-A48585C9637A}.Release|x86.Build.0 = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|x64.ActiveCfg = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|x64.Build.0 = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|x86.ActiveCfg = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Debug|x86.Build.0 = Debug|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|Any CPU.ActiveCfg = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|Any CPU.Build.0 = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|x64.ActiveCfg = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|x64.Build.0 = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|x86.ActiveCfg = Release|Any CPU
    {A775F37D-4AA6-47D8-8305-6DFDB843B347}.Release|x86.Build.0 = Release|Any CPU
  EndGlobalSection
EndGlobal")},
                { 
                    XFS.Path(@"c:\Solution\Project.WithPackages\Project.WithPackages.csproj"), 
                    new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.All"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Mvc"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Mvc.Core"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Server.IISIntegration"" Version=""1.0.0"" />
    <PackageReference Include=""System.Net.Http"" Version=""4.3.1"" />
    <PackageReference Include=""System.Net.Security"" Version=""4.3.0"" />
  </ItemGroup>
</Project>")
                },
                { 
                    XFS.Path(@"c:\Solution\Project.NoPackages\Project.NoPackages.csproj"), 
                    new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk""><ItemGroup></ItemGroup></Project>")
                },
                { 
                    XFS.Path(@"c:\Solution\Project.Vb\Project.Vb.vbproj"), 
                    new MockFileData(@"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.All"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Mvc"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Mvc.Core"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Server.IISIntegration"" Version=""1.0.0"" />
    <PackageReference Include=""System.Net.Http"" Version=""4.3.1"" />
    <PackageReference Include=""System.Net.Security"" Version=""4.3.0"" />
  </ItemGroup>
</Project>")
                },
            });
            Program.fileSystem = mockFileSystem;

            var exitCode = Program.Main(new string[] {
                XFS.Path(@"c:\Solution\Solution.sln"),
                "--noSerialNumber",
                "--out", XFS.Path(@"c:\Solution")
            });
            var bomContents = mockFileSystem.File.ReadAllText(XFS.Path(@"c:\Solution\bom.xml"));

            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<bom version=""1"" xmlns=""http://cyclonedx.org/schema/bom/1.1"">
  <components>
    <component type=""library"">
      <name>Microsoft.AspNetCore.All</name>
      <version>1.0.0</version>
      <purl>pkg:nuget/Microsoft.AspNetCore.All@1.0.0</purl>
    </component>
    <component type=""library"">
      <name>Microsoft.AspNetCore.Mvc</name>
      <version>1.0.0</version>
      <description><![CDATA[ASP.NET Core MVC is a web framework that gives you a powerful, patterns-based way to build dynamic websites and web APIs. ASP.NET Core MVC enables a clean separation of concerns and gives you full control over markup.]]></description>
      <licenses>
        <license>
          <url>http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm</url>
        </license>
      </licenses>
      <copyright>Copyright © Microsoft Corporation</copyright>
      <purl>pkg:nuget/Microsoft.AspNetCore.Mvc@1.0.0</purl>
      <externalReferences>
        <reference type=""website"">
          <url>http://www.asp.net/</url>
        </reference>
      </externalReferences>
    </component>
    <component type=""library"">
      <name>Microsoft.AspNetCore.Mvc.Core</name>
      <version>1.0.0</version>
      <description><![CDATA[ASP.NET Core MVC core components. Contains common action result types, attribute routing, application model conventions, API explorer, application parts, filters, formatters, model binding, and more.
Commonly used types:
Microsoft.AspNetCore.Mvc.AreaAttribute
Microsoft.AspNetCore.Mvc.BindAttribute
Microsoft.AspNetCore.Mvc.ControllerBase
Microsoft.AspNetCore.Mvc.FromBodyAttribute
Microsoft.AspNetCore.Mvc.FromFormAttribute
Microsoft.AspNetCore.Mvc.RequireHttpsAttribute
Microsoft.AspNetCore.Mvc.RouteAttribute]]></description>
      <licenses>
        <license>
          <url>http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm</url>
        </license>
      </licenses>
      <copyright>Copyright © Microsoft Corporation</copyright>
      <purl>pkg:nuget/Microsoft.AspNetCore.Mvc.Core@1.0.0</purl>
      <externalReferences>
        <reference type=""website"">
          <url>http://www.asp.net/</url>
        </reference>
      </externalReferences>
    </component>
    <component type=""library"">
      <name>Microsoft.AspNetCore.Server.IISIntegration</name>
      <version>1.0.0</version>
      <description><![CDATA[ASP.NET Core components for working with the IIS AspNetCoreModule.]]></description>
      <licenses>
        <license>
          <url>http://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm</url>
        </license>
      </licenses>
      <copyright>Copyright © Microsoft Corporation</copyright>
      <purl>pkg:nuget/Microsoft.AspNetCore.Server.IISIntegration@1.0.0</purl>
      <externalReferences>
        <reference type=""website"">
          <url>http://www.asp.net/</url>
        </reference>
      </externalReferences>
    </component>
    <component type=""library"">
      <name>System.Net.Http</name>
      <version>4.3.1</version>
      <description><![CDATA[Provides a programming interface for modern HTTP applications, including HTTP client components that allow applications to consume web services over HTTP and HTTP components that can be used by both clients and servers for parsing HTTP headers.

Commonly Used Types:
System.Net.Http.HttpResponseMessage
System.Net.Http.DelegatingHandler
System.Net.Http.HttpRequestException
System.Net.Http.HttpClient
System.Net.Http.MultipartContent
System.Net.Http.Headers.HttpContentHeaders
System.Net.Http.HttpClientHandler
System.Net.Http.StreamContent
System.Net.Http.FormUrlEncodedContent
System.Net.Http.HttpMessageHandler
 
When using NuGet 3.x this package requires at least version 3.4.]]></description>
      <licenses>
        <license>
          <url>http://go.microsoft.com/fwlink/?LinkId=329770</url>
        </license>
      </licenses>
      <copyright>© Microsoft Corporation.  All rights reserved.</copyright>
      <purl>pkg:nuget/System.Net.Http@4.3.1</purl>
      <externalReferences>
        <reference type=""website"">
          <url>https://dot.net/</url>
        </reference>
      </externalReferences>
    </component>
    <component type=""library"">
      <name>System.Net.Security</name>
      <version>4.3.0</version>
      <description><![CDATA[Provides types, such as System.Net.Security.SslStream, that uses SSL/TLS protocols to provide secure network communication between client and server endpoints.

Commonly Used Types:
System.Net.Security.SslStream
System.Net.Security.ExtendedProtectionPolicy
 
When using NuGet 3.x this package requires at least version 3.4.]]></description>
      <licenses>
        <license>
          <url>http://go.microsoft.com/fwlink/?LinkId=329770</url>
        </license>
      </licenses>
      <copyright>© Microsoft Corporation.  All rights reserved.</copyright>
      <purl>pkg:nuget/System.Net.Security@4.3.0</purl>
      <externalReferences>
        <reference type=""website"">
          <url>https://dot.net/</url>
        </reference>
      </externalReferences>
    </component>
  </components>
</bom>", bomContents);
        }
    }
}
