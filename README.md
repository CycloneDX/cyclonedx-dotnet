[![Build Status](https://travis-ci.org/CycloneDX/cyclonedx-dotnet.svg?branch=master)](https://travis-ci.org/CycloneDX/cyclonedx-dotnet)
[![License](https://img.shields.io/badge/license-Apache%202.0-brightgreen.svg)][License]
[![Website](https://img.shields.io/badge/https://-cyclonedx.org-blue.svg)](https://cyclonedx.org/)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=social&label=Follow)](https://twitter.com/CycloneDX_Spec)

CycloneDX module for .NET
=========

The CycloneDX module for .NET creates a valid CycloneDX bill-of-material document containing an aggregate of all project dependencies. CycloneDX is a lightweight BoM specification that is easily created, human readable, and simple to parse. The resulting bom.xml can be used with tools such as [OWASP Dependency-Track](https://dependencytrack.org/) for the continuous analysis of components.

Usage
-------------------

#### Installing

```bash
dotnet tool install --global dotnet-cyclonedx
```

If you already have a previous version of **dotnet-cyclonedx** installed, you can upgrade to the latest version using the following command:

```bash
dotnet tool update --global dotnet-cyclonedx
```

#### Options

```text
Usage: dotnet cyclonedx [path] -o [outputDirectory]

Arguments:
  Path                        The path to a .sln, .csproj or .vbproj file

Options:
  -o|--outputDirectory <OUTPUT_DIRECTORY> The directorty to write the BOM
  -?|-h|--help                            Show help information
```

#### Example
To run the **dotnet-cyclonedx** tool you need to specify a solution or project file. In case you pass a solution, the tool will aggregate all the projects.

```bash
dotnet cyclonedx YourSolution.sln
```

License
-------------------

Permission to modify and redistribute is granted under the terms of the Apache 2.0 license. See the [LICENSE] file for the full license.

[License]: https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE
