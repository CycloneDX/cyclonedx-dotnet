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
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using McMaster.Extensions.CommandLineUtils;

namespace CycloneDX {
    [Command(Name = "dotnet cyclonedx", FullName = "A .NET Core global tool to generate CycloneDX bill-of-material documents for use with Software Composition Analysis (SCA).")]
    class Program {
        [Argument(0, Name = "Path", Description = "The path to a .sln, .csproj or .vbproj file")]
        public string SolutionOrProjectFile { get; set; }

        [Option(Description = "The directory to write bom.xml", ShortName = "o")]
        string outputDirectory { get; }

        static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        async Task<int> OnExecute() {
            Console.WriteLine();

            if (string.IsNullOrEmpty(SolutionOrProjectFile)) {
                Console.Error.WriteLine($"A path is required");
                return 1;
            }

            if (string.IsNullOrEmpty(outputDirectory)) {
                Console.Error.WriteLine($"The output directory is required");
                return 1;
            }

            if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) {
                var solutionFile = Path.GetFullPath(SolutionOrProjectFile);
                return await AnalyzeSolutionAsync(solutionFile);
            }

            if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || 
                SolutionOrProjectFile.ToLowerInvariant().EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)) {
                var projectFile = Path.GetFullPath(SolutionOrProjectFile);
                return await AnalyzeProjectAsync(projectFile);
            }

            Console.Error.WriteLine($"Only .sln, .csproj and .vbproj files are supported");
            return 1;
        }

        /*
         * Analyzes all projects in a Solution.
         */
        async Task<int> AnalyzeSolutionAsync(string solutionFile) {
            if (!File.Exists(solutionFile)) {
                Console.Error.WriteLine($"Solution file \"{solutionFile}\" does not exist");
                return 1;
            }

            Console.WriteLine($"» Solution: {solutionFile}");
            Console.WriteLine();
            Console.WriteLine("  Getting projects".PadRight(64));
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);

            var solutionFolder = Path.GetDirectoryName(solutionFile);
            var projects = new List<string>();

            try {
                using (var reader = File.OpenText(solutionFile)) {
                    string line;

                    while ((line = await reader.ReadLineAsync()) != null) {
                        if (!line.StartsWith("Project", StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }
                        var regex = new Regex("(.*) = \"(.*?)\", \"(.*?)\"");
                        var match = regex.Match(line);
                        if (match.Success) {
                            var projectFile = Path.GetFullPath(Path.Combine(solutionFolder, match.Groups[3].Value));
                            projects.Add(projectFile);
                        }
                    }
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"  An unhandled exception occurred while getting the projects: {ex.Message}");
                return 1;
            }

            if (!projects.Any()) {
                Console.Error.WriteLine("  No projects found".PadRight(64));
                return 0;
            }

            Console.WriteLine($"  {projects.Count} project(s) found".PadRight(64));

            foreach (var project in projects) {
                Console.WriteLine();
                var ret = await AnalyzeProjectAsync(project);
                if (ret != 0) {
                    return ret;
                }
            }
            return 0;
        }

        /*
         * Creates a CycloneDX BoM from the list of Components and saves it to the specified directory.
         */
        XDocument CreateXmlDocument(List<Model.Component> components) {
            XNamespace ns = "http://cyclonedx.org/schema/bom/1.0";
            var doc = new XDocument();
            var bom = new XElement(ns + "bom", new XAttribute("version", "1"));
            var com = new XElement(ns + "components");
            foreach (var component in components) {
                var c = new XElement(ns + "component", new XAttribute("type", "library"));
                if (component.Group != null) {
                    c.Add(new XElement(ns + "group", component.Group));
                }
                if (component.Name != null) {
                    c.Add(new XElement(ns + "name", component.Name));
                }
                if (component.Version != null) {
                    c.Add(new XElement(ns + "version", component.Version));
                }
                if (component.Description != null) {
                    c.Add(new XElement(ns + "description", new XCData(component.Description)));
                }
                if (component.Scope != null) {
                    c.Add(new XElement(ns + "scope", component.Scope));
                }
                if (component.Hashes != null && component.Hashes.Count > 0) {
                    var h = new XElement(ns + "hashes");
                    foreach (var hash in component.Hashes) {
                        h.Add(new XElement(ns + "hash", hash.value, new XAttribute("alg", Model.AlgorithmExtensions.GetXmlString(hash.algorithm))));
                    }
                }
                if (component.Licenses != null && component.Licenses.Count > 0) {
                    var l = new XElement(ns + "licenses");
                    foreach (var license in component.Licenses) {
                        if (license.Id != null) {
                            l.Add(new XElement(ns + "license", new XElement(ns + "id", license.Id)));
                        } else if (license.Name != null) {
                            l.Add(new XElement(ns + "license", new XElement(ns + "name", license.Name)));
                        }
                    }
                }
                if (component.Copyright != null) {
                    c.Add(new XElement(ns + "copyright", component.Copyright));
                }
                if (component.Cpe != null) {
                    c.Add(new XElement(ns + "cpe", component.Cpe));
                }
                if (component.Purl != null) {
                    c.Add(new XElement(ns + "purl", component.Purl));
                }
                c.Add(new XElement(ns + "modified", false));
                com.Add(c);
            }
            bom.Add(com);
            doc.Add(bom);
            var bomFile = Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar + "bom.xml";
            Console.WriteLine("Writing to: " + bomFile);
            doc.Save(@bomFile);
            return doc;
        }

        /*
         * Analyzes a single Project.
         */
        async Task<int> AnalyzeProjectAsync(string projectFile) {
            var components = new List<Model.Component>();
            if (!File.Exists(projectFile)) {
                Console.Error.WriteLine($"Project file \"{projectFile}\" does not exist");
                return 1;
            }
            Console.WriteLine($"» Project: {projectFile}");
            Console.WriteLine();
            Console.WriteLine("  Getting packages".PadRight(64));
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);

            try {
                using (XmlReader reader = XmlReader.Create(projectFile)) {
                    while (reader.Read()) {
                        if (reader.IsStartElement()) {
                            switch (reader.Name) {
                                case "PackageReference":
                                    var component = new Model.Component();
                                    var packageName = reader["Include"];
                                    var packageVersion = reader["Version"];
                                    component.Name = packageName;
                                    component.Version = packageVersion;
                                    component.Purl = $"pkg:nuget/{packageName}@{packageVersion}";
                                    await RetrieveExtendedNugetAttributes(component);
                                    components.Add(component);
                                    break;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"  An unhandled exception occurred while getting the packages: {ex.Message}");
                return 1;
            }

            if (!components.Any()) {
                Console.Error.WriteLine("  No packages found".PadRight(64));
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("Creating CycloneDX BoM");
            CreateXmlDocument(components);
            return 0;
        }

        /*
         * Retrieves additional information for the specified Component from NuGet and 
         * updates the component.
         */
        async Task<int> RetrieveExtendedNugetAttributes(Model.Component component) {
            var url = "https://api.nuget.org/v3-flatcontainer/" 
                + component.Name + "/" + component.Version + "/" + component.Name + ".nuspec";

            Console.WriteLine("Retrieving " + component.Name + " " + component.Version);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            HttpResponseMessage response;

            try {
                response = await client.GetAsync(url);
            } catch (Exception ex) {
                Console.WriteLine($"  An unhandled exception occurred while querying nuget.org for additional package information: {ex.Message}");
                return 1;
            }

            var contentAsString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) {
                if ((int)response.StatusCode != 404) {
                    Console.WriteLine($"  An unhandled exception occurred while querying nuget.org for additional package information: {(int)response.StatusCode} {response.StatusCode} {contentAsString}");
                }
                return 1;
            }

            var doc = new XmlDocument();
            doc.LoadXml(contentAsString);
            var root = doc.DocumentElement;
            var metadata = root.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']");

            var authors = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'authors']");
            if (authors != null) {
                component.Publisher = authors.FirstChild.Value;
            }

            var copyright = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'copyright']");
            if (copyright != null) {
                component.Copyright = copyright.FirstChild.Value;
            }

            var title = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'title']");
            var summary = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'summary']");
            var description = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'description']");

            if (summary != null && summary.FirstChild.Value != null) {
                component.Description = summary.FirstChild.Value;
            } else if (description != null && description.FirstChild.Value != null) {
                component.Description = description.FirstChild.Value;
            } else if (title != null && title.FirstChild.Value != null) {
                component.Description = title.FirstChild.Value;
            }

            //TODO: nuspec authors thought it would be a good idea to publish the URL to the license
            //rather than the SPDX identifier of the license itself. Genius! NOT! nuspec will need to
            //change the spec if they wish to provide accurate license information in boms.
            /*
            var licenseUrl = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'licenseUrl']");
            if (licenseUrl != null && licenseUrl.FirstChild.Value != null) {
                var license = new Model.License();
                license.Name = licenseUrl.FirstChild.Value;
                component.Licenses.Add(license);
            }
            */

            return 0;
        }
    }
}
