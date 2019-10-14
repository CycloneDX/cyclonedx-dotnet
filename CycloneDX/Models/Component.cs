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

namespace CycloneDX.Models
{
    public class Component : IComparable<Component>
    {
        public string Publisher { get; set; }
        public string Group { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; }
        public List<Hash> Hashes { get; set; } = new List<Hash>();
        public List<License> Licenses { get; set; } = new List<License>();
        public string Copyright { get; set; }
        public string Cpe { get; set; }
        public string Purl { get; set; }
        public bool Modified { get; set; }
        public List<Component> Components { get; set; } = new List<Component>();
        public List<ExternalReference> ExternalReferences { get; set; } = new List<ExternalReference>();
        public string Type { get; set; }
        public HashSet<NugetPackage> Dependencies { get; set; } = new HashSet<NugetPackage>();

        public override bool Equals(object obj)
        {
            var other = obj as Component;
            return this.Equals(other);
        }

        public bool Equals(Component other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return this.Name == other.Name && this.Version == other.Version;
            }
        }

        public override int GetHashCode()
        {
            return Utils.GeneratePackageUrl(Name, Version).GetHashCode();
        }

        public int CompareTo(Component other)
        {
            if (other == null)
            {
                return 1;
            }
            else
            {
                var nameComparison = this.Name.CompareTo(other.Name);
                return nameComparison == 0
                    ? this.Version.CompareTo(other.Version)
                    : nameComparison;
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as Component;
            return CompareTo(other);
        }
    }
}