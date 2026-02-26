// This file is part of CycloneDX Tool for .NET
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using VerifyTests;
using VerifyXunit;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Global Verify configuration, applied once via a module initializer.
    ///
    /// Scrubs volatile fields from CycloneDX BOM output so snapshots are stable:
    ///   - serialNumber (UUID changes every run)
    ///   - timestamp (changes every run)
    ///   - tool version (changes between releases)
    ///   - hash values (may differ depending on local NuGet cache state)
    /// </summary>
    public static class VerifyConfig
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // Auto-accept snapshots when they don't exist yet (first run).
            // Set VERIFY_DISABLE_CLIP=1 in CI to prevent clipboard usage.
            VerifierSettings.AutoVerify();

            // Store snapshots in the Snapshots/ subfolder of the project directory
            Verifier.DerivePathInfo(
                (sourceFile, projectDirectory, type, method) =>
                    new PathInfo(
                        directory: System.IO.Path.Combine(projectDirectory, "Snapshots"),
                        typeName: type.Name,
                        methodName: method.Name));

            // Global scrubbing rules applied to every verified string
            VerifierSettings.ScrubLinesWithReplace(line =>
            {
                // serialNumber: urn:uuid:xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                line = Regex.Replace(
                    line,
                    @"urn:uuid:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                    "urn:uuid:{scrubbed}");

                // ISO-8601 timestamps
                line = Regex.Replace(
                    line,
                    @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})",
                    "{scrubbed-timestamp}");

                // Tool version numbers  e.g.  <version>5.4.0</version>  inside <tool> block
                // We replace any semver-like version string in the tool metadata element
                line = Regex.Replace(
                    line,
                    @"(<version>)\d+\.\d+\.\d+(?:\.\d+)?(?:-[^<]+)?(</version>)",
                    "$1{scrubbed-version}$2");

                // SHA-512 hashes (base64, ~88 chars ending in ==)
                line = Regex.Replace(
                    line,
                    @"(?:[A-Za-z0-9+/]{86,88}={0,2})",
                    "{scrubbed-hash}");

                return line;
            });
        }
    }
}
