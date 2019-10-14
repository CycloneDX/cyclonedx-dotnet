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

using System.Collections.Generic;
using System.Threading.Tasks;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class ComponentService
    {
        private INugetService _nugetService;

        public ComponentService(INugetService nugetService)
        {
            _nugetService = nugetService;
        }

        /// <summary>
        /// Recursively retrieve all components and their dependencies from NuGet packages
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <returns></returns>
        public async Task<HashSet<Component>> RecursivelyGetComponentsAsync(IEnumerable<NugetPackage> nugetPackges)
        {
            var components = new HashSet<Component>();

            // Initialize the queue with the current packages
            var packages = new Queue<NugetPackage>(nugetPackges);
            NugetPackage currentPackage;

            var visitedNugetPackages = new HashSet<NugetPackage>();

            while (packages.TryDequeue(out currentPackage))
            {
                var component = await _nugetService.GetComponentAsync(currentPackage);

                if (component == null) continue;

                components.Add(component);

                // Add unvisited NuGet package dependencies to the queue
                foreach (var dependency in component.Dependencies)
                {
                    if (!visitedNugetPackages.Contains(dependency))
                    {
                        packages.Enqueue(dependency);
                    }
                }

                // Add the current NuGet package to list of visited packages
                visitedNugetPackages.Add(currentPackage);
            }

            return components;
        }

    }
}