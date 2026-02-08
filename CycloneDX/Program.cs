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

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var SolutionOrProjectFile = new Argument<string>("path") { Description = "The path to a .sln, .slnf, .slnx, .csproj, .fsproj, .vbproj, .xsproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files.", Arity = ArgumentArity.ZeroOrOne };
            var framework = new Option<string>("--framework", "-tfm") { Description = "The target framework to use. If not defined, all will be aggregated." };

            var runtime = new Option<string>("--runtime", "-rt") { Description = "The runtime to use. If not defined, all will be aggregated." };
            var outputDirectory = new Option<string>("--output", "-o") { Description = "The directory to write the BOM" };
            var outputFilename = new Option<string>("--filename", "-fn") { Description = "Optionally provide a filename for the BOM (default: bom.xml or bom.json)" };
            var excludeDev = new Option<bool>("--exclude-dev", "-ed") { Description = "Exclude development dependencies from the BOM (see https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference)" };
            var excludetestprojects = new Option<bool>("--exclude-test-projects", "-t") { Description = "Exclude test projects from the BOM" };
            var baseUrl = new Option<string>("--url", "-u") { Description = "Alternative NuGet repository URL to https://<yoururl>/nuget/<yourrepository>/v3/index.json" };
            var baseUrlUS = new Option<string>("--baseUrlUsername", "-us") { Description = "Alternative NuGet repository username" };
            var baseUrlUSP = new Option<string>("--baseUrlUserPassword", "-usp") { Description = "Alternative NuGet repository username password/apikey" };
            var isPasswordClearText = new Option<bool>("--isBaseUrlPasswordClearText", "-uspct") { Description = "Alternative NuGet repository password is cleartext" };
            var scanProjectReferences = new Option<bool>("--recursive", "-rs") { Description = "To be used with a single project file, it will recursively scan project references of the supplied project file" };
            var noSerialNumber = new Option<bool>("--no-serial-number", "-ns") { Description = "Optionally omit the serial number from the resulting BOM" };
            var githubUsername = new Option<string>("--github-username", "-gu") { Description = "Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token" };
            var githubT = new Option<string>("--github-token", "-gt") { Description = "Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username" };
            var githubBT = new Option<string>("--github-bearer-token", "-gbt") { Description = "Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions" };
            var enableGithubLicenses = new Option<bool>("--enable-github-licenses", "-egl") { Description = "Enables GitHub license resolution" };
            var disablePackageRestore = new Option<bool>("--disable-package-restore", "-dpr") { Description = "Optionally disable package restore" };
            var disableHashComputation = new Option<bool>("--disable-hash-computation", "-dhc") { Description = "Optionally disable hash computation for packages" };
            var dotnetCommandTimeout = new Option<int>("--dotnet-command-timeout", "-dct") { Description = "dotnet command timeout in milliseconds (primarily used for long dotnet restore operations)", DefaultValueFactory = _ => 300000 };
            var baseIntermediateOutputPath = new Option<string>("--base-intermediate-output-path", "-biop") { Description = "Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated." };
            var importMetadataPath = new Option<string>("--import-metadata-path", "-imp") { Description = "Optionally provide a metadata template which has project specific details." };
            var setName = new Option<string>("--set-name", "-sn") { Description = "Override the autogenerated BOM metadata component name." };
            var setVersion = new Option<string>("--set-version", "-sv") { Description = "Override the default BOM metadata component version (defaults to 0.0.0)." };
            var includeProjectReferences = new Option<bool>("--include-project-references", "-ipr") { Description = "Include project references as components (can only be used with project files)." };
            var setType = new Option<Component.Classification>("--set-type", "-st") { Description = "Override the default BOM metadata component type (defaults to application).", DefaultValueFactory = _ => Component.Classification.Application };
            var setNugetPurl = new Option<bool>("--set-nuget-purl", null) { Description = "Override the default BOM metadata component bom ref and PURL as NuGet package." };
            var excludeFilter = new Option<string>("--exclude-filter", "-ef") { Description = "A comma separated list of dependencies to exclude in form 'name1@version1,name2@version2' or 'name1,name2' (to exclude all versions). Transitive dependencies will also be removed." };
            var outputFormat = new Option<OutputFileFormat>("--output-format", "-F") { Description = "Select the BOM output format: auto (default), xml, json, or unsafeJson (relaxed escaping)." };
            var specVersion = new Option<string>("--spec-version", "-spv") { Description = $"Which version of CycloneDX spec to use. [default: {SpecificationVersionHelpers.VersionString(SpecificationVersionHelpers.CurrentVersion)}]" };
            specVersion.AcceptOnlyFromAmong(Enum.GetValues<SpecificationVersion>().Select(SpecificationVersionHelpers.VersionString).ToArray());

            //Deprecated args for backward compatibility
            var outputDirectoryDeprecated = new Option<string>("--out", null) { Description = "(Deprecated use --output instead) The directory to write the BOM" };
            var jsonDeprecated = new Option<bool>("--json", null) { Description = "(Deprecated use --output-format instead) Output in JSON format" };

            RootCommand rootCommand = new RootCommand("A .NET Core global tool which creates CycloneDX Software Bill-of-Materials (SBOM) from .NET projects.")
            {
                SolutionOrProjectFile,
                framework,
                runtime,
                outputDirectory,
                outputFilename,
                excludeDev,
                excludetestprojects,
                baseUrl,
                baseUrlUS,
                baseUrlUSP,
                isPasswordClearText,
                scanProjectReferences,
                noSerialNumber,
                githubUsername,
                githubT,
                githubBT,
                enableGithubLicenses,
                disablePackageRestore,
                disableHashComputation,
                dotnetCommandTimeout,
                baseIntermediateOutputPath,
                importMetadataPath,
                includeProjectReferences,
                setName,
                setVersion,
                setType,
                setNugetPurl,
                specVersion,
                excludeFilter,
                outputFormat,
                outputDirectoryDeprecated,
                jsonDeprecated
            };

            ParseResult parseResult = rootCommand.Parse(args);


            if (parseResult.Errors.Count == 0 && parseResult.GetValue(SolutionOrProjectFile) is string)
            {

                RunOptions options = new RunOptions
                {
                    SolutionOrProjectFile = parseResult.GetValue(SolutionOrProjectFile),
                    runtime = parseResult.GetValue(runtime),
                    framework = parseResult.GetValue(framework),
                    outputDirectory = parseResult.GetValue(outputDirectory) ?? parseResult.GetValue(outputDirectoryDeprecated),
                    outputFilename = parseResult.GetValue(outputFilename),
                    excludeDev = parseResult.GetValue(excludeDev),
                    excludeTestProjects = parseResult.GetValue(excludetestprojects),
                    baseUrl = parseResult.GetValue(baseUrl),
                    baseUrlUserName = parseResult.GetValue(baseUrlUS),
                    baseUrlUSP = parseResult.GetValue(baseUrlUSP),
                    isPasswordClearText = parseResult.GetValue(isPasswordClearText),
                    scanProjectReferences = parseResult.GetValue(scanProjectReferences),
                    noSerialNumber = parseResult.GetValue(noSerialNumber),
                    githubUsername = parseResult.GetValue(githubUsername),
                    githubT = parseResult.GetValue(githubT),
                    githubBT = parseResult.GetValue(githubBT),
                    enableGithubLicenses = parseResult.GetValue(enableGithubLicenses),
                    disablePackageRestore = parseResult.GetValue(disablePackageRestore),
                    disableHashComputation = parseResult.GetValue(disableHashComputation),
                    dotnetCommandTimeout = parseResult.GetValue(dotnetCommandTimeout),
                    baseIntermediateOutputPath = parseResult.GetValue(baseIntermediateOutputPath),
                    importMetadataPath = parseResult.GetValue(importMetadataPath),
                    setName = parseResult.GetValue(setName),
                    setVersion = parseResult.GetValue(setVersion),
                    setType = parseResult.GetValue(setType),
                    setNugetPurl = parseResult.GetValue(setNugetPurl),
                    includeProjectReferences = parseResult.GetValue(includeProjectReferences),
                    DependencyExcludeFilter = parseResult.GetValue(excludeFilter),
                    outputFormat = parseResult.GetValue(jsonDeprecated) ? OutputFileFormat.Json : parseResult.GetValue(outputFormat),
                    specVersion = parseResult.GetValue(specVersion) != null
                        ? SpecificationVersionHelpers.Version(parseResult.GetValue(specVersion))
                        : null
                };

                Runner runner = new Runner();
                var taskStatus = await runner.HandleCommandAsync(options);
                return taskStatus;
            }
            else if (parseResult.Errors.Count == 0)
            {
                return parseResult.Invoke();
            }
            foreach (ParseError parseError in parseResult.Errors)
            {
                Console.Error.WriteLine(parseError.Message);
            }
            return 1;



        }
    }
}
