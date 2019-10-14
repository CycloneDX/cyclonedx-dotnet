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

using System.Diagnostics.CodeAnalysis;

namespace CycloneDX.Models
{
    public class ExternalReference
    {
        public string Url { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }

        public const string VCS = "vcs";
        public const string ISSUE_TRACKER = "issue-tracker";
        public const string WEBSITE = "website";
        public const string ADVISORIES = "advisories";
        public const string BOM = "bom";
        public const string MAILING_LIST = "mailing-list";
        public const string SOCIAL = "social";
        public const string CHAT = "chat";
        public const string DOCUMENTATION = "documentation";
        public const string SUPPORT = "support";
        public const string DISTRIBUTION = "distribution";
        public const string LICENSE = "license";
        public const string BUILD_META = "build-meta";
        public const string BUILD_SYSTEM = "build-system";
        public const string OTHER = "other";
    }
}
