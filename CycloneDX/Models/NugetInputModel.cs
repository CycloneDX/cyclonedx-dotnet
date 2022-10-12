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

namespace CycloneDX.Models
{
    public static class NugetInputFactory
    {
        public static NugetInputModel Create(string baseUrl, string baseUrlUserName, string baseUrlUserPassword,
            bool isPasswordClearText)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(baseUrlUserName) && !string.IsNullOrEmpty(baseUrlUserPassword))
            {
                return new NugetInputModel(baseUrl, baseUrlUserName, baseUrlUserPassword, isPasswordClearText);
            }

            return new NugetInputModel(baseUrl);
        }

    }

    public class NugetInputModel
    {
        public string nugetFeedUrl { get; set; }
        public string nugetUsername { get; set; }
        public string nugetPassword { get; set; }
        public bool IsPasswordClearText { get; set; }

        public NugetInputModel(string baseUrl)
        {
            nugetFeedUrl = baseUrl;
        }

        public NugetInputModel(string baseUrl, string baseUrlUserName, string baseUrlUserPassword,
            bool isPasswordClearText)
        {
            nugetFeedUrl = baseUrl;
            nugetUsername = baseUrlUserName;
            nugetPassword = baseUrlUserPassword;
            IsPasswordClearText = isPasswordClearText;
        }
    }
}
