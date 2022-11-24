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
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CycloneDX.Interfaces;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    public class DotnetUtilsService : IDotnetUtilsService
    {
        private Regex _sdkPathRegex = new Regex(@"(\S)+ \[(?<path>.*)\]");
        private readonly Regex _globalPackageCacheLocationPath = new Regex(@"global-packages: (?<path>.*)$");

        private IFileSystem _fileSystem;
        private readonly IDotnetCommandService _dotnetCommandService;

        public DotnetUtilsService(IFileSystem fileSystem, IDotnetCommandService dotNetCommandService)
        {
            _fileSystem = fileSystem;
            _dotnetCommandService = dotNetCommandService;
        }

        internal DotnetUtilsResult<string> GetNuGetFallbackFolderPath()
        {
            var commandResult = _dotnetCommandService.Run("--list-sdks");

            if (commandResult.Success)
            {
                var match = _sdkPathRegex.Match(commandResult.StdOut);
                if (match.Success)
                {
                    var fallbackPath = _fileSystem.Path.Combine(match.Groups["path"].ToString(), "NuGetFallbackFolder");
                    if (_fileSystem.Directory.Exists(fallbackPath))
                    {
                        return new DotnetUtilsResult<string>
                        {
                            Result = fallbackPath
                        };
                    }
                    else
                    {
                        return new DotnetUtilsResult<string>();
                    }
                }
            }

            return new DotnetUtilsResult<string>
            {
                ErrorMessage = commandResult.StdErr
            };
        }

        internal DotnetUtilsResult<string> GetGlobalPackagesCacheLocation()
        {
            var commandResult = _dotnetCommandService.Run("nuget locals global-packages --list");

            if (commandResult.Success)
            {
                var match = _globalPackageCacheLocationPath.Match(commandResult.StdOut);
                if (match.Success)
                {
                    return new DotnetUtilsResult<string>
                    {
                        // on Windows (at least) the path will have a carriage return
                        Result = match.Groups["path"].ToString().Trim()
                    };
                }
            }

            return new DotnetUtilsResult<string>
            {
                ErrorMessage = commandResult.StdErr
            };
        }

        private static string CombineErrorMessages(string currentErrorMessage, string additionalErrorMessage)
        {
            if (additionalErrorMessage == null) { return currentErrorMessage; }

            if (currentErrorMessage.Length == 0)
            {
                return additionalErrorMessage;
            }

            return $"{currentErrorMessage}\n{additionalErrorMessage}";
        }

        public DotnetUtilsResult<List<string>> GetPackageCachePaths()
        {
            var result = new List<string>();
            var cacheLocation = GetGlobalPackagesCacheLocation();
            var fallbackLocation = GetNuGetFallbackFolderPath();

            if (cacheLocation.Success && fallbackLocation.Success)
            {
                result.Add(cacheLocation.Result);
                if (fallbackLocation.Result != null) { result.Add(fallbackLocation.Result); }

                return new DotnetUtilsResult<List<string>>
                {
                    Result = result
                };
            }
            else
            {
                var errorMessage = "";
                errorMessage = CombineErrorMessages(errorMessage, cacheLocation.ErrorMessage);
                errorMessage = CombineErrorMessages(errorMessage, fallbackLocation.ErrorMessage);
                return new DotnetUtilsResult<List<string>>
                {
                    ErrorMessage = errorMessage
                };
            }
        }

        public DotnetUtilsResult Restore(string path, string framework, string runtime)
        {
            var arguments = "restore";
            if (!string.IsNullOrEmpty(framework)) arguments = $"{arguments} -p:TargetFramework={framework}";
            if (runtime != null) arguments = $"{arguments} -r {runtime}";
            if (!string.IsNullOrEmpty(path)) arguments = $"{arguments} \"{path}\"";

            var commandResult = _dotnetCommandService.Run(arguments);

            if (commandResult.Success)
            {
                return new DotnetUtilsResult();
            }
            else
            {
                return new DotnetUtilsResult
                {
                    // dotnet restore only outputs to std out, not std err
                    ErrorMessage = commandResult.StdOut
                };
            }
        }
    }
}
