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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using McMaster.Extensions.CommandLineUtils;

namespace CycloneDX {
    [Command(Name = "dotnet cyclonedx", FullName = "A .NET Core global tool to generate CycloneDX bill-of-material documents for use with Software Composition Analysis (SCA).")]
    class Program {
        [Argument(0, Name = "Path", Description = "The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files")]
        public string SolutionOrProjectFile { get; set; }

        [Option(Description = "The directory to write bom.xml", ShortName = "o", LongName = "out")]
        string outputDirectory { get; }

        [Option(Description = "Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required).", ShortName = "u", LongName = "url")]
        string baseUrl { get; set; }

        Dictionary<string, Model.Component> dependencyMap = new Dictionary<string, Model.Component>();

        static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        async Task<int> OnExecute() {
            if (baseUrl == null) {
                baseUrl = "https://api.nuget.org/v3-flatcontainer/";
            }
            Console.WriteLine();
            FileAttributes attr = File.GetAttributes(@SolutionOrProjectFile);

            if (string.IsNullOrEmpty(SolutionOrProjectFile)) {
                Console.Error.WriteLine($"A path is required");
                return 1;
            }

            if (string.IsNullOrEmpty(outputDirectory)) {
                Console.Error.WriteLine($"The output directory is required");
                return 1;
            }

            int returnCode = 1;
            if (SolutionOrProjectFile.ToLowerInvariant().EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) {
                string file = Path.GetFullPath(SolutionOrProjectFile);
                returnCode = await AnalyzeSolutionAsync(file);

            } else if (isSupportedProjectType(SolutionOrProjectFile)) {
                string file = Path.GetFullPath(SolutionOrProjectFile);
                returnCode = await AnalyzeProjectAsync(file);

            } else if (SolutionOrProjectFile.ToLowerInvariant().Equals("packages.config", StringComparison.OrdinalIgnoreCase)) {
                string file = Path.GetFullPath(SolutionOrProjectFile);
                returnCode = await AnalyzeProjectAsync(file);

            } else if (attr.HasFlag(FileAttributes.Directory)) {
                string path = Path.GetFullPath(SolutionOrProjectFile);
                returnCode = await AnalyzeDirectoryAsync(path);
            }

            if (returnCode == 0){
                Console.WriteLine();
                Console.WriteLine("Creating CycloneDX BoM");
                CreateXmlDocument(dependencyMap);
                return 0;
            } else {
                Console.Error.WriteLine($"Only .sln, .csproj, .vbproj, and packages.config files are supported");
                return 1;
            }
        }

        bool isSupportedProjectType(string filename) {
            return filename.ToLowerInvariant().EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                filename.ToLowerInvariant().EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);
        }

        /*
         * Analyzes all projects in a Solution.
         */
        async Task<int> AnalyzeSolutionAsync(string solutionFile) {
            if (!File.Exists(solutionFile)) {
                Console.Error.WriteLine($"Solution file \"{solutionFile}\" does not exist");
                return 1;
            }
            Console.WriteLine();
            Console.WriteLine($"» Solution: {solutionFile}");
            Console.WriteLine("  Getting projects".PadRight(64));
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
                            var relativeProjectPath = match.Groups[3].Value.Replace('\\', Path.DirectorySeparatorChar);
                            var projectFile = Path.GetFullPath(Path.Combine(solutionFolder, relativeProjectPath));
                            if (isSupportedProjectType(projectFile) && File.Exists(projectFile)) {
                                projects.Add(projectFile);
                            }
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
         * Recursively analyzes a directory for packages.config files.
         */
        async Task<int> AnalyzeDirectoryAsync(string projectPath) {
            string path = new FileInfo(projectPath).Directory.FullName;
            string[] files = Directory.GetFiles(path, "packages.config", SearchOption.AllDirectories);
            foreach (string file in files) {
                int val = await AnalyzeProjectAsync(file);
                if (val != 0) {
                    return val;
                }
            }
            return 0;
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
            Console.WriteLine();
            Console.WriteLine($"» Analyzing: {projectFile}");
            Console.WriteLine("  Getting packages".PadRight(64));
            try {
                using (XmlReader reader = XmlReader.Create(projectFile)) {
                    while (reader.Read()) {
                        if (reader.IsStartElement()) {
                            switch (reader.Name) {
                                case "PackageReference": { // For managed NuGet dependencies defined in a project (csproj/vbproj)
                                        var component = new Model.Component();
                                        var packageName = reader["Include"];
                                        var packageVersion = reader["Version"];
                                        component.Name = packageName;
                                        component.Version = packageVersion;
                                        component.Purl = generatePackageUrl(packageName, packageVersion);
                                        int val = await RetrieveExtendedNugetAttributes(component, true);
                                        if (val != 0) {
                                            // An error occurred while fetching the unmanaged dependency from NuGet.
                                            // Add the dependency to the component dictionary.
                                            AddPreventDuplicates(component);
                                        }
                                        components.Add(component);
                                        break;
                                    }
                                case "package": { // For managed NuGet dependencies defined in a seperate packages.config
                                        var component = new Model.Component();
                                        var packageName = reader["id"];
                                        var packageVersion = reader["version"];
                                        component.Name = packageName;
                                        component.Version = packageVersion;
                                        component.Purl = generatePackageUrl(packageName, packageVersion);
                                        int val = await RetrieveExtendedNugetAttributes(component, true);
                                        if (val != 0) {
                                            // An error occurred while fetching the unmanaged dependency from NuGet.
                                            // Add the dependency to the component dictionary.
                                            AddPreventDuplicates(component);
                                        }
                                        components.Add(component);
                                        break;
                                    }
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
            }
            return 0;
        }

        /*
         * Creates a CycloneDX BoM from the list of Components and saves it to the specified directory.
         */
        XDocument CreateXmlDocument(Dictionary<string, Model.Component> components) {
            XNamespace ns = "http://cyclonedx.org/schema/bom/1.0";
            var doc = new XDocument();
            doc.Declaration = new XDeclaration("1.0", "utf-8", null);
            var bom = new XElement(ns + "bom", new XAttribute("version", "1"));
            var com = new XElement(ns + "components");
            foreach (KeyValuePair<string, Model.Component> item in components) {
                var component = item.Value;
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
					c.Add(l);
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
            using (var writer = new StreamWriter(bomFile, false, new UTF8Encoding(false))) {
                doc.Save(writer);
            }
            return doc;
        }

        /*
         * Retrieves additional information for the specified Component from NuGet and 
         * updates the component.
         */
        async Task<int> RetrieveExtendedNugetAttributes(Model.Component component, bool followTransitive) {
            var url = baseUrl + component.Name + "/" + component.Version + "/" + component.Name + ".nuspec";
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
            component.Publisher = getNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'authors']");
            component.Copyright = getNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'copyright']");
            var title = getNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'title']");
            var summary = getNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'summary']");
            var description = getNodeValue(metadata, "/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'description']");
            if (summary != null) {
                component.Description = summary;
            } else if (description != null) {
                component.Description = description;
            } else if (title != null) {
                component.Description = title;
            }

			// Utilize the new license expression field present in more recent packages
			// TODO: Need to have more robust parsing to support composite expressions seen in (https://github.com/NuGet/Home/wiki/Packaging-License-within-the-nupkg#project-properties)
			var licenseNode = metadata.SelectSingleNode("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'license']");
			if (licenseNode?.Attributes["type"].Value == "expression")
			{
				var licenses = licenseNode.FirstChild.Value
					.Replace("AND", ";")
					.Replace("OR", ";")
					.Replace("WITH", ";")
					.Replace("+", "")
					.Split(';').ToList();
				foreach (var license in licenses)
				{
					component.Licenses.Add(new Model.License
					{
						Id = license.Trim(),
						Name = license.Trim()
					});
				}
			}

			// As a final step (and before optionally fetching transitive dependencies), add the component to the dictionary.
			AddPreventDuplicates(component);

			if (followTransitive) {
                var dependencies = metadata.SelectNodes("/*[local-name() = 'package']/*[local-name() = 'metadata']/*[local-name() = 'dependencies']/*[local-name() = 'dependency']");
                foreach (XmlNode dependency in dependencies) {
                    var id = dependency.Attributes["id"];
                    var version = dependency.Attributes["version"];
                    if (id != null && version != null) {
                        var transitive = new Model.Component();
                        transitive.Name = id.Value;
                        transitive.Version = version.Value;
                        transitive.Purl = generatePackageUrl(transitive.Name, transitive.Version);
                        await RetrieveExtendedNugetAttributes(transitive, false);
                    }
                }
            }
            return 0;
        }

        /*
         * Creates a PackageURL from the specified package name and version. 
         */ 
        string generatePackageUrl(string packageName, string packageVersion) {
            if (packageName == null || packageVersion == null) {
                return null;
            }
            return $"pkg:nuget/{packageName}@{packageVersion}";
        }

        /*
         * Helper method which performs null checking when querying for the value of an XML node.
         */
        string getNodeValue(XmlNode xmlNode, string xpath) {
            var node = xmlNode.SelectSingleNode(xpath);
            if (node != null && node.FirstChild != null) {
                return node.FirstChild.Value;
            }
            return null;
        }

        /*
         * Adds a Component to the map using the PackageURL of the component as the key.
         */
        void AddPreventDuplicates(Model.Component component) {
            if (component.Purl != null && !dependencyMap.ContainsKey(component.Purl)) {
                dependencyMap.Add(component.Purl, component);
            }
        }

    }
}
