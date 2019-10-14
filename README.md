[![Build Status](https://travis-ci.org/CycloneDX/cyclonedx-dotnet.svg?branch=master)](https://travis-ci.org/CycloneDX/cyclonedx-dotnet)
[![License](https://img.shields.io/badge/license-Apache%202.0-brightgreen.svg)][License]
[![NuGet Version](https://img.shields.io/nuget/v/CycloneDX.svg)](https://www.nuget.org/packages/CycloneDX/)
[![Website](https://img.shields.io/badge/https://-cyclonedx.org-blue.svg)](https://cyclonedx.org/)
[![Group Discussion](https://img.shields.io/badge/discussion-groups.io-blue.svg)](https://groups.io/g/CycloneDX)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=social&label=Follow)](https://twitter.com/CycloneDX_Spec)

CycloneDX module for .NET
=========

The CycloneDX module for .NET creates a valid CycloneDX bill-of-material document containing an aggregate of all project dependencies. CycloneDX is a lightweight BoM specification that is easily created, human readable, and simple to parse. The resulting bom.xml can be used with tools such as [OWASP Dependency-Track](https://dependencytrack.org/) for the continuous analysis of components.

Usage
-------------------

#### Installing

```bash
dotnet tool install --global CycloneDX
```

If you already have a previous version of **CycloneDX** installed, you can upgrade to the latest version using the following command:

```bash
dotnet tool update --global CycloneDX
```

#### Options

```text
Usage: CycloneDX [path] -o [outputDirectory]

Arguments:
  Path            The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files.

Options:
  -o|--out <DIR>        The directorty to write the BOM
  -u|--url <URL>        Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required).
  -r|--recursive        To be used with a single project file, it will recursively scan project references of the supplied .csproj.	
 -ns|--noSerialNumber   Do not generate bom serial number
  -?|-h|--help          Show help information
```

#### Examples
To run the **CycloneDX** tool you need to specify a solution or project file. In case you pass a solution, the tool will aggregate all the projects.

The following will create a BOM from a solution and all projects defined within:
```bash
dotnet CycloneDX YourSolution.sln -o /output/path
```

The following will recursively scan the directory structure for packages.config and create a BOM:
```bash
dotnet CycloneDX /path/to/project -o /output/path
```

The following will recursively scan the project references of the supplied .csproj file, and create a BOM of all package references from all included projects:
```bash
dotnet CycloneDX /path/to/project/MyProject.csproj -o /output/path -r
```

License
-------------------

Permission to modify and redistribute is granted under the terms of the Apache 2.0 license. See the [LICENSE] file for the full license.

[License]: https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE
