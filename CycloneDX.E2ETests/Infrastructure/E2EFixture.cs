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
using System.Threading.Tasks;
using Xunit;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Combined xUnit collection fixture that:
    ///  1. Publishes the CycloneDX tool once (via <see cref="ToolFixture"/>)
    ///  2. Starts a BaGetter NuGet server (via <see cref="NuGetServerFixture"/>)
    ///  3. Pushes the shared package vocabulary to BaGetter
    ///
    /// Shared across all test classes in the "E2E" collection.
    /// </summary>
    public sealed class E2EFixture : IAsyncLifetime
    {
        private readonly ToolFixture _toolFixture = new();
        private readonly NuGetServerFixture _nugetServer = new();

        /// <summary>Feed URL of the BaGetter container, e.g. http://localhost:PORT/v3/index.json</summary>
        public string NuGetFeedUrl => _nugetServer.FeedUrl;

        /// <summary>Ready-to-use runner that invokes the compiled CycloneDX tool.</summary>
        internal CycloneDxRunner Runner { get; private set; }

        /// <summary>
        /// Pushes an additional package to BaGetter for per-test custom scenarios.
        /// </summary>
        public Task PushPackageAsync(byte[] nupkgBytes) =>
            _nugetServer.PushPackageAsync(nupkgBytes);

        public async ValueTask InitializeAsync()
        {
            // Publish the tool and start BaGetter in parallel
            await Task.WhenAll(
                _toolFixture.PublishAsync(),
                _nugetServer.StartAsync()
            ).ConfigureAwait(false);

            // Push the shared package vocabulary
            await _nugetServer.PushVocabularyPackagesAsync().ConfigureAwait(false);

            Runner = new CycloneDxRunner(_toolFixture.ToolDllPath);
        }

        public async ValueTask DisposeAsync()
        {
            await _nugetServer.DisposeAsync().ConfigureAwait(false);
            await _toolFixture.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// xUnit collection definition — all E2E test classes share one <see cref="E2EFixture"/>.
    /// </summary>
    [CollectionDefinition("E2E")]
    public sealed class E2ECollection : ICollectionFixture<E2EFixture> { }
}
