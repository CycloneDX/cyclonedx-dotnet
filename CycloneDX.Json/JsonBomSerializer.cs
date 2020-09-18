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

using System.Diagnostics.Contracts;
using System.Text.Json;

using Bom = CycloneDX.Models.Bom;

namespace CycloneDX.Json
{

    public static class JsonBomSerializer
    {
        public static string Serialize(Bom bom)
        {
            Contract.Requires(bom != null);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true,
            };

            options.Converters.Add(new LicenseConverter());

            var jsonBom = JsonSerializer.Serialize(bom, options);

            return jsonBom;
        }
    }
}
