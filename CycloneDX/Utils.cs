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

namespace CycloneDX
{
    public static class Utils
    {
        /*
         * Creates a PackageURL from the specified package name and version. 
         */
        public static string GeneratePackageUrl(string packageName, string packageVersion) {
            if (packageName == null || packageVersion == null) {
                return null;
            }
            return $"pkg:nuget/{packageName}@{packageVersion}";
        }

        public static bool IsSupportedProjectType(string filename) {
            return filename.ToLowerInvariant().EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                filename.ToLowerInvariant().EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}