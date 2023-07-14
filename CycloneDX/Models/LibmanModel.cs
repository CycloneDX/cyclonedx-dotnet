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

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CycloneDX.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LibmanProvider
    {
        unpkg,
        cdnjs,
        jsdelivr,
        filesystem
    }

    public class LibmanModel
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("defaultProvider")]
        public LibmanProvider DefaultProvider { get; set; }

        [JsonPropertyName("libraries")]
        public List<LibraryModel> Libraries { get; set; } = new List<LibraryModel>();
    }

    public class LibraryModel
    {
        private const string VERSION_REGEX = @"^(\@(?<ns>.*?)\/)?(?<name>.*?)\@(?<version>[0-9\.]+)$";

        private string _library;

        [JsonPropertyName("library")]
        public string Library
        {
            get { return _library; }
            set
            {
                _library = value;
                ParseLibrary();
            }
        }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("files")]
        public string[] Files { get; set; }

        [JsonPropertyName("provider")]
        public LibmanProvider? Provider { get; set; }

        public string Namespace { get; private set; }

        public string Name { get; private set; }

        public string Version { get; private set; }


        private void ParseLibrary()
        {
            var regex = new Regex(VERSION_REGEX, RegexOptions.IgnoreCase);
            var match = regex.Match(_library);

            if (match.Success)
            {
                Name = match.Groups["name"].Value;
                Version = match.Groups["version"].Value;

                if (match.Groups.ContainsKey("ns"))
                {
                    Namespace = match.Groups["ns"].Value;
                }
            }
        }
    }
}
