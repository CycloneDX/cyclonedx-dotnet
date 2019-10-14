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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using CycloneDX.Models;
using CycloneDX.Services;

namespace CycloneDX {
    [Command(Name = "dotnet cyclonedx", FullName = "A .NET Core global tool which creates CycloneDX Software Bill-of-Materials (SBoM) from .NET projects.")]
    class Program {
        [Argument(0, Name = "Path", Description = "The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files")]
        public string SolutionOrProjectFile { get; set; }

        [Option(Description = "The directory to write bom.xml", ShortName = "o", LongName = "out")]
        string outputDirectory { get; }

        [Option(Description = "Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required).", ShortName = "u", LongName = "url")]
        string baseUrl { get; set; }

        [Option(Description = "To be used with a single project file, it will recursively scan project references of the supplied .csproj.", ShortName = "r", LongName = "recursive")]
        bool scanProjectReferences { get; set; }

        [Option(Description = "Optionally omit the serial number from the resulting BOM.", ShortName = "ns", LongName = "noSerialNumber")]
        bool noSerialNumber { get; set; }

        static internal IFileSystem fileSystem = new FileSystem();
        static internal HttpClient httpClient = new HttpClient();

        static internal int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        async Task<int> OnExecute() {
            Console.WriteLine();

            // check parameter values
            if (string.IsNullOrEmpty(SolutionOrProjectFile)) {

                Console.Error.WriteLine($"A path is required");
                return (int)ExitCode.SolutionOrProjectFileParameterMissing;
            }

            if (string.IsNullOrEmpty(outputDirectory)) {
                Console.Error.WriteLine($"The output directory is required");
                return (int)ExitCode.OutputDirectoryParameterMissing;
            }

            // instantiate services
            var fileDiscoveryService = new FileDiscoveryService(Program.fileSystem);
            var nugetService = new NugetService(Program.httpClient, baseUrl);
            var packagesFileService = new PackagesFileService(Program.fileSystem);
            var projectFileService = new ProjectFileService(Program.fileSystem);
            var solutionFileService = new SolutionFileService(Program.fileSystem);
            var packages = new HashSet<NugetPackage>();

            // determine what we are analyzing and do the analysis
            var fullSolutionOrProjectFilePath = Program.fileSystem.Path.GetFullPath(SolutionOrProjectFile);
            var attr = Program.fileSystem.File.GetAttributes(fullSolutionOrProjectFilePath);

            if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                packages = await solutionFileService.GetSolutionNugetPackages(fullSolutionOrProjectFilePath);
            }
            else if (Utils.IsSupportedProjectType(SolutionOrProjectFile) && scanProjectReferences)
            {
                packages = await projectFileService.RecursivelyGetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath);
            }
            else if (Utils.IsSupportedProjectType(SolutionOrProjectFile))
            {
                packages = await projectFileService.GetProjectNugetPackagesAsync(fullSolutionOrProjectFilePath);
            }
            else if (Program.fileSystem.Path.GetFileName(SolutionOrProjectFile).ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase))
            {
                packages = await packagesFileService.GetNugetPackagesAsync(fullSolutionOrProjectFilePath);

            } 
            else if (attr.HasFlag(FileAttributes.Directory))
            {
                packages = await packagesFileService.RecursivelyGetNugetPackagesAsync(fullSolutionOrProjectFilePath);
            }
            else
            {
                Console.Error.WriteLine($"Only .sln, .csproj, .vbproj, and packages.config files are supported");
                return (int)ExitCode.InvalidOptions;
            }

            // get all the components from the NuGet packages
            var components = new HashSet<Component>();
            foreach (var package in packages)
            {
                var component = await nugetService.GetComponentAsync(package);
                if (component != null) components.Add(component);
            }

            // create the BOM
            Console.WriteLine();
            Console.WriteLine("Creating CycloneDX BoM");
            var bomXml = BomService.CreateXmlDocument(components, noSerialNumber);

            // check if the output directory exists and create it if needed
            var bomPath = Program.fileSystem.Path.GetFullPath(outputDirectory);
            if (!Program.fileSystem.Directory.Exists(bomPath))
                Program.fileSystem.Directory.CreateDirectory(bomPath);

            // write the BOM to disk
            var bomFile = Program.fileSystem.Path.Combine(bomPath, "bom.xml");
            Console.WriteLine("Writing to: " + bomFile);
            using (var fileStream = Program.fileSystem.FileStream.Create(bomFile, FileMode.Create))
            using (var writer = new StreamWriter(fileStream, new UTF8Encoding(false))) {
                bomXml.Save(writer);
            }

            return 0;
        }
    }
}
