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
using System.Diagnostics.CodeAnalysis;

namespace CycloneDX.Models
{
    // This suppression should maybe be revisited when/if a CycloneDX library is published
    [SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes")]
    public class NugetPackage : IComparable
    {
        public string Name { get; set; }
        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as NugetPackage;
            return this.Equals(other);
        }

        public bool Equals(NugetPackage other)
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

        public int CompareTo(NugetPackage other)
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
            var other = obj as NugetPackage;
            return CompareTo(other);
        }
    }
}