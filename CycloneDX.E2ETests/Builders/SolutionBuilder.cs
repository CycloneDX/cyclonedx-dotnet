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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CycloneDX.E2ETests.Infrastructure;

namespace CycloneDX.E2ETests.Builders
{
    /// <summary>
    /// Describes a NuGet package reference for a project.
    /// </summary>
    internal sealed class PackageRef
    {
        public string Id { get; }
        public string Version { get; }
        /// <summary>When true, adds PrivateAssets="all" — makes it a dev/build-only dependency.</summary>
        public bool IsDevDependency { get; }

        public PackageRef(string id, string version, bool isDevDependency = false)
        {
            Id = id;
            Version = version;
            IsDevDependency = isDevDependency;
        }
    }

    /// <summary>
    /// Fluent builder for a single .csproj project within a solution.
    /// </summary>
    internal sealed class ProjectOptions
    {
        internal string Name { get; }
        internal string TargetFramework { get; private set; } = "net8.0";
        internal List<PackageRef> Packages { get; } = new();
        internal List<string> ProjectReferences { get; } = new();
        internal string RawXmlBlocks { get; private set; } = "";
        internal bool IsTestProject { get; private set; }

        public ProjectOptions(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public ProjectOptions WithTargetFramework(string tfm)
        {
            TargetFramework = tfm;
            return this;
        }

        public ProjectOptions AddPackage(string id, string version, bool devDependency = false)
        {
            Packages.Add(new PackageRef(id, version, devDependency));
            return this;
        }

        /// <summary>
        /// Adds a project reference by relative path (e.g. "../MyLib/MyLib.csproj").
        /// </summary>
        public ProjectOptions AddProjectReference(string relativePath)
        {
            ProjectReferences.Add(relativePath);
            return this;
        }

        /// <summary>
        /// Marks this project as a test project (IsTestProject = true in the .csproj).
        /// </summary>
        public ProjectOptions AsTestProject()
        {
            IsTestProject = true;
            return this;
        }

        /// <summary>
        /// Injects raw XML into the .csproj for exotic scenarios not covered by the fluent API.
        /// The XML is inserted verbatim inside the &lt;Project&gt; element.
        /// </summary>
        public ProjectOptions WithRawXml(string xml)
        {
            RawXmlBlocks += "\n" + xml;
            return this;
        }
    }

    /// <summary>
    /// Fluent builder for a solution containing one or more projects.
    /// Creates real .sln / .csproj files on disk and runs <c>dotnet restore</c>
    /// so that <c>project.assets.json</c> files are generated automatically.
    /// </summary>
    internal sealed class SolutionBuilder
    {
        private readonly string _solutionName;
        private readonly List<ProjectOptions> _projects = new();
        private string _nugetFeedUrl;

        public SolutionBuilder(string solutionName = "TestSolution")
        {
            _solutionName = solutionName;
        }

        /// <summary>Adds a project, configured via the <paramref name="configure"/> action.</summary>
        public SolutionBuilder AddProject(string name, Action<ProjectOptions> configure = null)
        {
            var opts = new ProjectOptions(name);
            configure?.Invoke(opts);
            _projects.Add(opts);
            return this;
        }

        /// <summary>
        /// Override the NuGet feed URL used during restore.
        /// If not called here, the feed URL must be supplied to <see cref="BuildAsync"/>.
        /// </summary>
        public SolutionBuilder WithNuGetFeed(string feedUrl)
        {
            _nugetFeedUrl = feedUrl;
            return this;
        }

        /// <summary>
        /// Writes all project files to a new temp directory, restores packages,
        /// and returns a <see cref="BuiltSolution"/> ready for tool invocation.
        /// </summary>
        public async Task<BuiltSolution> BuildAsync(string nugetFeedUrl = null)
        {
            var feedUrl = nugetFeedUrl ?? _nugetFeedUrl
                ?? throw new InvalidOperationException("A NuGet feed URL must be supplied.");

            var dir = new TempDirectory();
            try
            {
                // Write NuGet.Config so restore uses only our BaGetter feed
                WriteNuGetConfig(dir.Path, feedUrl);

                // Write each project
                foreach (var proj in _projects)
                {
                    WriteProject(dir.Path, proj);
                }

                // Write solution file if there is more than one project or we always want a .sln
                var slnPath = WriteSolution(dir.Path);

                // dotnet restore
                var (exitCode, stdOut, stdErr) = await ToolFixture.RunProcessAsync(
                    "dotnet",
                    $"restore \"{slnPath}\" --no-cache /nodeReuse:false",
                    workingDir: dir.Path
                ).ConfigureAwait(false);

                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"dotnet restore failed (exit {exitCode}):\n{stdErr}\n{stdOut}");
                }

                return new BuiltSolution(dir, slnPath, _projects.Select(p => p.Name).ToList());
            }
            catch
            {
                dir.Dispose();
                throw;
            }
        }

        private static void WriteNuGetConfig(string dir, string feedUrl)
        {
            var xml = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="bagetter" value="{feedUrl}" allowInsecureConnections="true" />
                  </packageSources>
                </configuration>
                """;
            File.WriteAllText(Path.Combine(dir, "NuGet.Config"), xml, Encoding.UTF8);
        }

        private static void WriteProject(string solutionDir, ProjectOptions proj)
        {
            var projDir = Path.Combine(solutionDir, proj.Name);
            Directory.CreateDirectory(projDir);

            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <TargetFramework>{proj.TargetFramework}</TargetFramework>");
            if (proj.IsTestProject)
            {
                sb.AppendLine("    <IsTestProject>true</IsTestProject>");
            }
            sb.AppendLine("  </PropertyGroup>");

            if (proj.Packages.Count > 0)
            {
                sb.AppendLine("  <ItemGroup>");
                foreach (var pkg in proj.Packages)
                {
                    if (pkg.IsDevDependency)
                        sb.AppendLine($"    <PackageReference Include=\"{pkg.Id}\" Version=\"{pkg.Version}\" PrivateAssets=\"all\" />");
                    else
                        sb.AppendLine($"    <PackageReference Include=\"{pkg.Id}\" Version=\"{pkg.Version}\" />");
                }
                sb.AppendLine("  </ItemGroup>");
            }

            if (proj.ProjectReferences.Count > 0)
            {
                sb.AppendLine("  <ItemGroup>");
                foreach (var pref in proj.ProjectReferences)
                {
                    sb.AppendLine($"    <ProjectReference Include=\"{pref}\" />");
                }
                sb.AppendLine("  </ItemGroup>");
            }

            if (!string.IsNullOrWhiteSpace(proj.RawXmlBlocks))
            {
                sb.AppendLine(proj.RawXmlBlocks);
            }

            sb.AppendLine("</Project>");

            File.WriteAllText(
                Path.Combine(projDir, $"{proj.Name}.csproj"),
                sb.ToString(),
                Encoding.UTF8);
        }

        private string WriteSolution(string dir)
        {
            var slnPath = Path.Combine(dir, $"{_solutionName}.sln");
            var slnGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio Version 17");

            var projectGuids = new List<(string Guid, string Name)>();
            foreach (var proj in _projects)
            {
                var projGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                projectGuids.Add((projGuid, proj.Name));
                sb.AppendLine($"Project(\"{{9A19103F-16F7-4668-BE54-9A1E7A4F7556}}\") = \"{proj.Name}\", \"{proj.Name}\\{proj.Name}.csproj\", \"{projGuid}\"");
                sb.AppendLine("EndProject");
            }

            sb.AppendLine("Global");
            sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
            sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
            sb.AppendLine("\tEndGlobalSection");
            sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            foreach (var (guid, _) in projectGuids)
            {
                sb.AppendLine($"\t\t{guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                sb.AppendLine($"\t\t{guid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                sb.AppendLine($"\t\t{guid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                sb.AppendLine($"\t\t{guid}.Release|Any CPU.Build.0 = Release|Any CPU");
            }
            sb.AppendLine("\tEndGlobalSection");
            sb.AppendLine($"\tGlobalSection(ExtensibilityGlobals) = postSolution");
            sb.AppendLine($"\t\tSolutionGuid = {slnGuid}");
            sb.AppendLine("\tEndGlobalSection");
            sb.AppendLine("EndGlobal");

            File.WriteAllText(slnPath, sb.ToString(), Encoding.UTF8);
            return slnPath;
        }
    }

    /// <summary>
    /// A built solution on disk — provides paths for tool invocation.
    /// Dispose to delete the temp directory.
    /// </summary>
    internal sealed class BuiltSolution : IDisposable
    {
        private readonly TempDirectory _dir;

        public string RootDir => _dir.Path;
        public string SolutionFile { get; }
        public IReadOnlyList<string> ProjectNames { get; }

        /// <summary>Returns the path to a specific project's .csproj file.</summary>
        public string ProjectFile(string projectName) =>
            Path.Combine(_dir.Path, projectName, $"{projectName}.csproj");

        /// <summary>A fresh temp output directory for BOM output (caller must dispose separately).</summary>
        public TempDirectory CreateOutputDir() => new();

        internal BuiltSolution(TempDirectory dir, string solutionFile, List<string> projectNames)
        {
            _dir = dir;
            SolutionFile = solutionFile;
            ProjectNames = projectNames;
        }

        public void Dispose() => _dir.Dispose();
    }
}
