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
using System.Xml;
using CycloneDX.Models;

namespace CycloneDX.Services
{
    /// <summary>
    /// Properties and items of one project file, from two sources that are each read at most once:
    /// evaluated by MSBuild (sees imports such as Directory.Build.props, conditions and property
    /// references), and declared in the project file XML (the fallback when the evaluation fails).
    /// </summary>
    internal sealed class ProjectEvaluation
    {
        private readonly string _projectFilePath;
        private readonly IFileSystem _fileSystem;
        private readonly Lazy<EvaluatedProject> _evaluated;
        private readonly Lazy<XmlDocument> _projectXml;

        /// <param name="evaluate">Evaluates the project, null when no evaluation is available.</param>
        /// <param name="reportEvaluationError">Called with the error when the evaluation fails.</param>
        public ProjectEvaluation(string projectFilePath, IFileSystem fileSystem,
            Func<DotnetUtilsResult<EvaluatedProject>> evaluate, Action<string> reportEvaluationError)
        {
            _projectFilePath = projectFilePath;
            _fileSystem = fileSystem;
            _evaluated = new Lazy<EvaluatedProject>(() => Evaluate(evaluate, reportEvaluationError));
            _projectXml = new Lazy<XmlDocument>(LoadProjectXml);
        }

        /// <summary>Whether MSBuild evaluated the project.</summary>
        public bool IsEvaluated => _evaluated.Value != null;

        /// <summary>The evaluated value, or the declared one when the evaluation has none.</summary>
        public string GetProperty(string name) => GetEvaluatedProperty(name) ?? GetDeclaredProperty(name);

        /// <summary>The value evaluated by MSBuild; null when it is empty or the project was not evaluated.</summary>
        public string GetEvaluatedProperty(string name)
        {
            return _evaluated.Value != null && _evaluated.Value.Properties.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }

        /// <summary>The Include values of the evaluated items, or of the declared ones when the project was not evaluated.</summary>
        public IEnumerable<string> GetItems(string itemType)
        {
            return _evaluated.Value != null && _evaluated.Value.Items.TryGetValue(itemType, out var items)
                ? items
                : GetDeclaredItems(itemType);
        }

        /// <summary>
        /// The first value declared in a top-level PropertyGroup of the project file, in any namespace;
        /// null when it is not declared. Conditions, imports and property references are not evaluated.
        /// </summary>
        public string GetDeclaredProperty(string name)
        {
            return SelectElements($"/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='{name}']")
                .Select(element => element.InnerText)
                .FirstOrDefault();
        }

        /// <summary>The Include values of the items declared anywhere in the project file, in any namespace.</summary>
        public IEnumerable<string> GetDeclaredItems(string itemType)
        {
            return SelectElements($"//*[local-name()='{itemType}']")
                .Select(element => element.GetAttribute("Include"))
                .Where(include => include.Length > 0);
        }

        private static EvaluatedProject Evaluate(
            Func<DotnetUtilsResult<EvaluatedProject>> evaluation, Action<string> reportEvaluationError)
        {
            if (evaluation is null)
            {
                return null;
            }
            var result = evaluation();
            if (result is { Success: true, Result: not null })
            {
                return result.Result;
            }
            reportEvaluationError?.Invoke(result?.ErrorMessage);
            return null;
        }

        private XmlDocument LoadProjectXml()
        {
            var document = new XmlDocument();
            using var fileStream = _fileSystem.FileStream.New(_projectFilePath, FileMode.Open, FileAccess.Read);
            document.Load(fileStream);
            return document;
        }

        private IEnumerable<XmlElement> SelectElements(string xpath) => _projectXml.Value.SelectNodes(xpath).OfType<XmlElement>();
    }
}
