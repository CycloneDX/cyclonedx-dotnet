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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CycloneDX.Models
{
    public enum DependencyType
    {
        Package,
        Project
    }

    [SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes")]
    public class DotnetDependency : IComparable
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsDirectReference { get; set; }
        public bool IsDevDependency { get; set; }
        public DependencyType DependencyType { get; set; }
        public Component.ComponentScope? Scope { get; set; }
        public string Path { get; set; }


        public Dictionary<string, string> Dependencies { get; set; } //key: name ~ value: version

        public override bool Equals(object obj)
        {
            var other = obj as DotnetDependency;
            return this.Equals(other);
        }

        public bool Equals(DotnetDependency other)
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

        public int CompareTo(DotnetDependency other)
        {
            if (other == null)
            {
                return 1;
            }
            else
            {
                var nameComparison = string.Compare(this.Name, other.Name, StringComparison.Ordinal);
                return nameComparison == 0
                    ? string.Compare(this.Version, other.Version, StringComparison.Ordinal)
                    : nameComparison;
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as DotnetDependency;
            return CompareTo(other);
        }

        public override string ToString()
        {
            return $"{Name}@{Version}"; 
        }
    }
}
