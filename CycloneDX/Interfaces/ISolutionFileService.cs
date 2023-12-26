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
using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX.Interfaces
{
    public interface ISolutionFileService
    {
        Task<HashSet<string>> GetSolutionProjectReferencesAsync(string solutionFilePath);
        Task<HashSet<DotnetDependency>> GetSolutionDotnetDependencys(string solutionFilePath, string baseIntermediateOutputPath, bool excludeTestProjects, bool excludeDev, string framework, string runtime);
    }
}
