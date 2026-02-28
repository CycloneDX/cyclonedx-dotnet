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
using System.Net.Http;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CycloneDX.E2ETests.Infrastructure
{
    /// <summary>
    /// Manages the lifecycle of a BaGetter NuGet server running in a Testcontainer.
    /// Push packages via <see cref="PushPackageAsync"/> before the fixture is fully ready.
    /// </summary>
    internal sealed class NuGetServerFixture : IAsyncDisposable
    {
        private const int BaGetterPort = 8080;
        private const string BaGetterImage = "bagetter/bagetter:latest";
        public const string ApiKey = "cdx-e2e-test-key";

        private IContainer _container;
        private HttpClient _http;

        public string FeedUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(BaGetterPort)}/v3/index.json";
        public string PushUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(BaGetterPort)}/api/v2/package";

        public async Task StartAsync()
        {
            _container = new ContainerBuilder(BaGetterImage)
                .WithPortBinding(BaGetterPort, assignRandomHostPort: true)
                .WithEnvironment("ApiKey", ApiKey)
                .WithEnvironment("Storage__Type", "FileSystem")
                .WithEnvironment("Storage__Path", "/data")
                .WithEnvironment("Database__Type", "Sqlite")
                .WithEnvironment("Database__ConnectionString", "Data Source=/data/bagetter.db")
                .WithEnvironment("Search__Type", "Database")
                .WithWaitStrategy(
                    Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPort(BaGetterPort)
                            .ForPath("/v3/index.json")))
                .Build();

            await _container.StartAsync().ConfigureAwait(false);

            _http = new HttpClient();
        }

        /// <summary>
        /// Pushes a .nupkg byte array to the BaGetter instance.
        /// </summary>
        public async Task PushPackageAsync(byte[] nupkgBytes)
        {
            using var content = new ByteArrayContent(nupkgBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Put, PushUrl)
            {
                Headers = { { "X-NuGet-ApiKey", ApiKey } },
                Content = content
            };

            var response = await _http.SendAsync(request).ConfigureAwait(false);

            // 409 Conflict = package already exists (idempotent)
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return;
            }

            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Pushes all packages in the shared vocabulary.
        /// Called once during fixture startup.
        /// </summary>
        public async Task PushVocabularyPackagesAsync()
        {
            // TestPkg.A 1.0.0 — simple package, no deps
            await PushPackageAsync(NupkgBuilder.Build("TestPkg.A", "1.0.0")).ConfigureAwait(false);

            // TestPkg.A 2.0.0 — second version for multi-version tests
            await PushPackageAsync(NupkgBuilder.Build("TestPkg.A", "2.0.0")).ConfigureAwait(false);

            // TestPkg.B 1.0.0 — depends on TestPkg.A 1.0.0 (transitive scenario)
            await PushPackageAsync(NupkgBuilder.Build(
                "TestPkg.B", "1.0.0",
                dependencies: new[] { new NupkgDependency("TestPkg.A", "1.0.0") }
            )).ConfigureAwait(false);

            // TestPkg.C 1.0.0 — simple package, no deps (for multi-dep tests)
            await PushPackageAsync(NupkgBuilder.Build("TestPkg.C", "1.0.0")).ConfigureAwait(false);

            // TestPkg.Dev 1.0.0 — intended to be used as a dev/build dependency
            await PushPackageAsync(NupkgBuilder.Build("TestPkg.Dev", "1.0.0")).ConfigureAwait(false);

            // TestPkg.Transitive 1.0.0 — used only as a transitive dep
            await PushPackageAsync(NupkgBuilder.Build("TestPkg.Transitive", "1.0.0")).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _http?.Dispose();
            if (_container != null)
            {
                await _container.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
