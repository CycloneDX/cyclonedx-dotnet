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

using System;
using System.IO;
using System.Linq;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// A temporary directory that is deleted when disposed.
    /// </summary>
    internal sealed class TempDirectory : IDisposable
    {
        private readonly string _baseTempPath;

        public string Path { get; }

        public TempDirectory()
        {
            // Build the path exclusively from GetTempPath() and GetRandomFileName() —
            // no external input is involved.  GetFullPath canonicalises the result so
            // that CodeQL's taint-tracking sees a fully-resolved, anchor-validated path.
            _baseTempPath = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            var name = "cdx-e2e-" + System.IO.Path.GetRandomFileName();
            Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(_baseTempPath, name));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Combines <paramref name="parts"/> with the temp directory root and validates
        /// the result stays within this directory (guards against path-traversal).
        /// </summary>
        public string Combine(params string[] parts)
        {
            var combined = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray()));
            if (!combined.StartsWith(Path, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Path traversal detected: '{combined}' is outside '{Path}'.");
            }
            return combined;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
