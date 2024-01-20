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
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class BuildalyzerService : IBuildalyzerService
    {
        AnalyzerManager _manager;
        AnalyzerManager manager
        {
            get
            {
                if (_manager == null)
                    InitializeAnalyzer(null);
                return _manager;
            }
        }
        Dictionary<string, IAnalyzerResults> cachedResults { get; } = new Dictionary<string, IAnalyzerResults>();
        string tempPath { get; }

        public BuildalyzerService()
        {
            tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);            

        }

        ~ BuildalyzerService()
        {
            Directory.Delete(tempPath, true);
        }

        public void InitializeAnalyzer(string solutionFilePath)
        {
            if (_manager != null)
            {
                throw new InvalidOperationException("Analyzer has already been initialized. It cannot be initialized twice.");
            }

            _manager = string.IsNullOrEmpty(solutionFilePath)
                     ? new AnalyzerManager()
                     : new AnalyzerManager(solutionFilePath);

            _manager.SetGlobalProperty("UseArtifactsOutput", "true");
            _manager.SetGlobalProperty("ArtifactsPath", tempPath);
        }

          
                


        private IAnalyzerResults getAnalyzerResults(string projectFilePath)
        {
            if (!cachedResults.TryGetValue(projectFilePath, out var results))
            {
                var project = manager.GetProject(projectFilePath);
                results = project.Build();
                cachedResults.Add(projectFilePath, results);
            }
            return results;
        }

        public bool IsTestProject(string projectFilePath)
        {
            var buildResults = getAnalyzerResults(projectFilePath);
            var isTestProjectSDK = buildResults.SelectMany(x => x.Properties)
                    .Any(x => x.Key.Equals("IsTestProject", StringComparison.OrdinalIgnoreCase) && x.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

            var isTestProjectLegacy = buildResults.SelectMany(x => x.Properties)
                    .Any(x => x.Key.Equals("TestProjectType", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value));

            return isTestProjectSDK || isTestProjectLegacy;
        }

        public HashSet<string> GetProjectPathsOfSolution()
        {            
            return manager.Projects.Select(p => p.Key).ToHashSet();
        }
    }
}
