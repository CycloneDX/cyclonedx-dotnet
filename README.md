[![Build Status](https://github.com/CycloneDX/cyclonedx-dotnet/workflows/.NET%20Core%20CI/badge.svg)](https://github.com/CycloneDX/cyclonedx-dotnet/actions?workflow=.NET+Core+CI)
[![Docker Image](https://img.shields.io/badge/docker-image-brightgreen?style=flat&logo=docker)](https://hub.docker.com/r/cyclonedx/cyclonedx-dotnet)
[![License](https://img.shields.io/badge/license-Apache%202.0-brightgreen.svg)][License]
[![NuGet Version](https://img.shields.io/nuget/v/CycloneDX.svg)](https://www.nuget.org/packages/CycloneDX/)
![Nuget](https://img.shields.io/nuget/dt/CycloneDX.svg)
[![Website](https://img.shields.io/badge/https://-cyclonedx.org-blue.svg)](https://cyclonedx.org/)
[![Slack Invite](https://img.shields.io/badge/Slack-Join-blue?logo=slack&labelColor=393939)](https://cyclonedx.org/slack/invite)
[![Group Discussion](https://img.shields.io/badge/discussion-groups.io-blue.svg)](https://groups.io/g/CycloneDX)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=social&label=Follow)](https://twitter.com/CycloneDX_Spec)

# CycloneDX module for .NET

The CycloneDX module for .NET creates a valid CycloneDX bill-of-material document containing an aggregate of all project dependencies. CycloneDX is a lightweight BOM specification that is easily created, human readable, and simple to parse.

This module runs on 
*   .NET Core 3.1 
*   .NET 6.0.

This module no longer runs on

*   .NET Core 2.1
*   .NET5 
*   see https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core for more infomation

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
dotnet CycloneDX <path> -o <OUTPUT_DIRECTORY>
```

#### Execution via Docker

```bash
docker run cyclonedx/cyclonedx-dotnet [OPTIONS] <path>
```

#### Options

```text
Usage: dotnet CycloneDX [options] <path>

Arguments:
  path                                              The path to a .sln, .csproj, .vbproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files

Options:
  -o|--out <OUTPUT_DIRECTORY>                                            The directory to write the BOM
  -f|--filename <OUTPUT_FILENAME>                                        Optionally provide a filename for the BOM (default: bom.xml or bom.json)
  -j|--json                                                              Produce a JSON BOM instead of XML
  -d|--exclude-dev                                                       Exclude development dependencies from the BOM
  -t|--exclude-test-projects                                             Exclude test projects from the BOM
  -u|--url <BASE_URL>                                                    Alternative NuGet repository URL to v3-flatcontainer API (a trailing slash is required)
  -r|--recursive                                                         To be used with a single project file, it will recursively scan project references of the supplied .csproj
  -ns|--no-serial-number                                                 Optionally omit the serial number from the resulting BOM
  -gu|--github-username <GITHUB_USERNAME>                                Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token
  -gt|--github-token <GITHUB_TOKEN>                                      Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username
  -gbt|--github-bearer-token <GITHUB_BEARER_TOKEN>                       Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions
  -dgl|--disable-github-licenses                                         Optionally disable GitHub license resolution
  -dpr|--disable-package-restore                                         Optionally disable package restore
  -dhc|--disable-hash-computation                                        Optionally disable hash computation for packages
  -biop|--base-intermediate-output-path <BASE_INTERMEDIATE_OUTPUT_PATH>  Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated.
  -imp|--import-metadata-path <METADATA_TEMPLATE>                        Optionally provide a metadata template which has project specific details
  -?|-h|--help                                                           Show help information
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

Project [metadata](https://cyclonedx.org/docs/1.2/#type_metadata) **template example**

```xml
<?xml version="1.0" encoding="utf-8"?>
<bom xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" serialNumber="urn:uuid:087d0712-f591-4995-ba76-03f1c5c48884" version="1" xmlns="http://cyclonedx.org/schema/bom/1.2">
  <metadata>
    <component type="application" bom-ref="pkg:nuget/CycloneDX@1.3.0">
      <name>CycloneDX</name>
      <version>1.3.0</version>
      <description>
        <![CDATA[The [CycloneDX module](https://github.com/CycloneDX/cyclonedx-dotnet) for .NET creates a valid CycloneDX bill-of-material document containing an aggregate of all project dependencies. CycloneDX is a lightweight BOM specification that is easily created, human readable, and simple to parse.]]>
      </description>
      <licenses>
        <license>
          <name>Apache License 2.0</name>
          <id>Apache-2.0</id>
        </license>
      </licenses>
      <purl>pkg:nuget/CycloneDX@1.3.0</purl>
    </component>
  </metadata>
</bom>
``` 

_Update the data and import it within a build pipeline e.g. create the file using a script and add also dynamic data (version, timestamp, ...)_ 

#### GitHub License Resolution

SPDX license IDs can be resolved for packages that reference a supported license
file in a GitHub repository.

The GitHub license API has an unauthenticated call limit of 60 calls per hour.
To ensure consistent output if a rate limit is exceeded BOM generation will
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

## Contributing

Pull requests are welcome. But please read the
[CycloneDX contributing guidelines](https://github.com/CycloneDX/.github/blob/master/CONTRIBUTING.md) first.

To build and test the solution locally you should have .NET Core 3.1 or .NET 6.0
installed. Standard commands like `dotnet build` and `dotnet test` work.

Alternatively, you can use VS Code and the included devcontainer configuration
to work in a pre-configured docker image. (You will also need the "Remote - Containers"
extension and Docker)

It is generally expected that pull requests will include relevant tests.
Tests are automatically run on Windows, MacOS and Linux for every pull request.
And build warnings will break the build.

If you are having trouble debugging a test that is failing for a platform you
don't have access to please us know.

Thanks to [Gitpod](https://gitpod.io/) there is a really easy way of creating
a ready to go development environment with VS Code. You can open a Gitpod
hosted development environment in your browser.

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/CycloneDX/cyclonedx-dotnet)
