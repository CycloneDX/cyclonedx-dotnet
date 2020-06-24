[![Build Status](https://github.com/CycloneDX/cyclonedx-dotnet/workflows/.NET%20Core%20CI/badge.svg)](https://github.com/CycloneDX/cyclonedx-dotnet/actions?workflow=.NET+Core+CI)
[![Docker Image](https://img.shields.io/badge/docker-image-brightgreen?style=flat&logo=docker)](https://hub.docker.com/r/cyclonedx/cyclonedx-dotnet)
[![License](https://img.shields.io/badge/license-Apache%202.0-brightgreen.svg)][License]
[![NuGet Version](https://img.shields.io/nuget/v/CycloneDX.svg)](https://www.nuget.org/packages/CycloneDX/)
[![Website](https://img.shields.io/badge/https://-cyclonedx.org-blue.svg)](https://cyclonedx.org/)
[![Slack Invite](https://img.shields.io/badge/Slack-Join-blue?logo=slack&labelColor=393939)](https://cyclonedx.org/slack/invite)
[![Group Discussion](https://img.shields.io/badge/discussion-groups.io-blue.svg)](https://groups.io/g/CycloneDX)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=social&label=Follow)](https://twitter.com/CycloneDX_Spec)

# CycloneDX module for .NET


The CycloneDX module for .NET creates a valid CycloneDX bill-of-material document containing an aggregate of all project dependencies. CycloneDX is a lightweight BOM specification that is easily created, human readable, and simple to parse.

This module runs on [.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet-core/2.1) and [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1).

## Usage

CycloneDX for .NET is distributed via NuGet and Docker Hub. 

#### Installing via NuGet

```bash
dotnet tool install --global CycloneDX
```

If you already have a previous version of **CycloneDX** installed, you can upgrade to the latest version using the following command:

```bash
dotnet tool update --global CycloneDX
```

#### Execution via DotNet

```bash
dotnet CycloneDX [OPTIONS]
```

#### Execution via Docker

```bash
docker run cyclonedx/cyclonedx-dotnet [OPTIONS]
```

#### Options

```text
Usage: CycloneDX [path] -o [outputDirectory]

Arguments:
  Path            The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files.

Options:
  -o|--out <DIR>                The directorty to write the BOM
  -j|--json                     Produce a JSON BOM instead of XML
  -u|--url <URL>                Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required).
  -r|--recursive                To be used with a single project file, it will recursively scan project references of the supplied .csproj.	
 -ns|--noSerialNumber           Do not generate bom serial number
 -gu|--githubUsername           Optionally provide a GitHub username for license resolution (see notes below)
 -gt|--githubToken              Optionally provide a GitHub personal access token for license resolution
 -gbt|--githubBearerToken       Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions.
 -dgl|--disableGithubLicenses   Optionally disable GitHub license resolution
  -?|-h|--help                  Show help information
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

#### GitHub License Resolution

SPDX license IDs can be resolved for packages that reference a supported license
file in a GitHub repository.

The GitHub license API has an unauthenticated call limit of 60 calls per hour.
To ensure consistent output if a rate limit is exceeded BoM generation will
fail. If you start hitting rate limits you will need to generate a personal
access token and provide this, and your username, when running CycloneDX.

To generate a token go to
[Personal access tokens](https://github.com/settings/tokens) under
`Settings / Developer setings`. From there select the option to
[Generate new token](https://github.com/settings/tokens/new). No special token
permissions are required.

Due to current limitations in the GitHub API licenses will only be resolved for
master branch license references.

## License

Permission to modify and redistribute is granted under the terms of the Apache 2.0 license. See the [LICENSE] file for the full license.

[License]: https://github.com/CycloneDX/cyclonedx-dotnet/blob/master/LICENSE
