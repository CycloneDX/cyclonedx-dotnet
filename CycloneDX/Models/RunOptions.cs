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

using System.Globalization;

namespace CycloneDX.Models
{
    public class RunOptions
    {
        public string SolutionOrProjectFile { get; set; }
        public string runtime { get; set; }
        public string framework { get; set; }
        public string outputDirectory { get; set; }
        public string outputFilename { get; set; }
        public bool excludeDev { get; set; }
        public bool excludeTestProjects { get; set; }
        public bool includeProjectReferences { get; set; }
        public string baseUrl { get; set; }
        public string baseUrlUserName { get; set; }
        public string baseUrlUSP { get; set; }
        public bool isPasswordClearText { get; set; }
        public bool scanProjectReferences { get; set; }
        public bool noSerialNumber { get; set; }
        public string githubUsername { get; set; }
        public string githubT { get; set; }
        public string githubBT { get; set; }
        public bool enableGithubLicenses { get; set; }
        public bool disablePackageRestore { get; set; }
        public bool disableHashComputation { get; set; }
        public int dotnetCommandTimeout { get; set; } = 30000;
        public string baseIntermediateOutputPath { get; set; }
        public string importMetadataPath { get; set; }
        public string setName { get; set; }
        public string setVersion { get; set; }
        public Component.Classification setType { get; set; } = Component.Classification.Null;
        public bool setNugetPurl { get; set; }
        public string DependencyExcludeFilter { get; set; }
        public OutputFileFormat outputFormat { get; set; }
        public SpecificationVersion? specVersion { get; set; }

    }
}
