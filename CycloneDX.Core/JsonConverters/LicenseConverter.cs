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
using System.Diagnostics.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

using License = CycloneDX.Models.License;

namespace CycloneDX.JsonConverters
{

    public class LicenseConverter : JsonConverter<License>
    {
        public override License Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            License value,
            JsonSerializerOptions options)
        {
            Contract.Requires(writer != null);
            Contract.Requires(value != null);

            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(value.Id))
            {
                writer.WritePropertyName("id");
                writer.WriteStringValue(value.Id);
            }
            else if (!string.IsNullOrEmpty(value.Name))
            {
                writer.WritePropertyName("name");
                writer.WriteStringValue(value.Name);
            }

            if (!string.IsNullOrEmpty(value.Url))
            {
                writer.WritePropertyName("url");
                writer.WriteStringValue(value.Url);
            }

            writer.WriteEndObject();
        }
    }
}
