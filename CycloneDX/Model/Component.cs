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
namespace CycloneDX.Model {

    public class Component {
        public string Publisher { get; set; }
        public string Group { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; }
        public List<Hash> Hashes { get; set; }
        public List<License> Licenses { get; set; }
        public string Copyright { get; set; }
        public string Cpe { get; set; }
        public string Purl { get; set; }
        public bool Modified { get; set; }
        public List<Component> Components { get; set; }
        public List<ExternalReference> ExternalReferences { get; set; }
        public string Type { get; set; }

        public Component() {
            Hashes = new List<Hash>();
            Licenses = new List<License>();
            ExternalReferences = new List<ExternalReference>();
            Components = new List<Component>();
        }
    }

}