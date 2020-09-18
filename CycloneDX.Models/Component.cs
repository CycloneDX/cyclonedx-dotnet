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
using System.Diagnostics.CodeAnalysis;

namespace CycloneDX.Models
{
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    // This suppression should maybe be revisited when/if a CycloneDX library is published
    [SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes")]
    public class Component : IComparable<Component>
    {
        public string Publisher { get; set; }
        public string Group { get; set; }
        public string Type { get; set; } = "library";
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; }
        public List<ComponentLicense> Licenses { get; set; } = new List<ComponentLicense>();
        public string Copyright { get; set; }
        public string Purl { get; set; }
        public List<ExternalReference> ExternalReferences { get; set; } = new List<ExternalReference>();

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
                var nameComparison = string.Compare(this.Name.ToUpperInvariant(), other.Name.ToUpperInvariant(), StringComparison.Ordinal);
                return nameComparison == 0
                    ? string.Compare(this.Version, other.Version, StringComparison.Ordinal)
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