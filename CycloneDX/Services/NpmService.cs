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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class NpmService : ILibmanService
    {
        public static readonly string BaseUrl = "https://registry.npmjs.org";

        private readonly HttpClient _httpClient;

        public NpmService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Component> GetComponentAsync(LibmanPackage package)
        {
            var url = string.IsNullOrEmpty(package.Namespace) ?
                $"{BaseUrl}/{package.Name}/" :
                $"{BaseUrl}/@{package.Namespace}/{package.Name}/";

            try
            {
                var response = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                var model = JsonSerializer.Deserialize<NpmModel>(response);

                if (model != null)
                    return CreateComponent(model, package.Version);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Call to NPM API failed with status code {ex.StatusCode} and message {ex.Message}.");
            }

            return null;
        }

        private static Component CreateComponent(NpmModel model, string version)
        {
            var component = new Component
            {
                Name = model.Name,
                Version = version,
                Description = model.Description,
                // Scope = scope, ?
                Purl = Utils.GeneratePackageUrl(PackageType.Libman, model.Name, version),
                Type = Component.Classification.Library,
                Licenses = new List<LicenseChoice>(),
                ExternalReferences = new List<ExternalReference>()
            };

            component.BomRef = component.Purl;

            if (model.Author != null)
                component.Author = model.Author.Name;

            if (!string.IsNullOrEmpty(model.License))
                component.Licenses.Add(new LicenseChoice
                {
                    License = new License { Id = model.License }
                });

            if (!string.IsNullOrEmpty(model.Homepage))
                component.ExternalReferences.Add(new ExternalReference
                {
                    Type = ExternalReference.ExternalReferenceType.Website,
                    Url = model.Homepage
                });

            return component;
        }
    }
}
