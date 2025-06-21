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
*   .NET 8.0
*   .NET 9.0

This module no longer runs on

*   .NET Core 2.1
*   .NET Core 3.1
*   .NET 5.0 
*   .NET 6.0
*   .NET 7.0
*   see https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core for more information

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

#### Execution  

```bash
dotnet-CycloneDX <path> -o <OUTPUT_DIRECTORY>
```  

> **Note:** If you encounter a "command not found" error after installation, try restarting your terminal. Also, ensure that the `~/.dotnet/tools` directory is included in your `PATH`.

#### Execution via Docker

```bash
docker run cyclonedx/cyclonedx-dotnet [OPTIONS] <path>
```

#### Options

```text
Usage:
  dotnet-CycloneDX <path> [options]

Arguments:
  <path>  The path to a .sln, .csproj, .fsproj, .vbproj, .xsproj, or packages.config file or the path to a directory which will be recursively analyzed for packages.config files.

Options:
  -tfm, --framework <framework>                                                The target framework to use. If not defined, all will be aggregated.
  -rt, --runtime <runtime>                                                     The runtime to use. If not defined, all will be aggregated.
  -o, --output <output>                                                        The directory to write the BOM
  -fn, --filename <filename>                                                   Optionally provide a filename for the BOM (default: bom.xml or bom.json)
  -ef, --exclude-filter <exclude-filter>                                       A comma separated list of dependencies to exclude in form 'name1@version1,name2@version2'. Transitive dependencies will also be removed.
  -ed, --exclude-dev                                                           Exclude development dependencies from the BOM (see https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference)
  -t, --exclude-test-projects                                                  Exclude test projects from the BOM
  -u, --url <url>                                                              Alternative NuGet repository URL to https://<yoururl>/nuget/<yourrepository>/v3/index.json
  -us, --baseUrlUsername <baseUrlUsername>                                     Alternative NuGet repository username
  -usp, --baseUrlUserPassword <baseUrlUserPassword>                            Alternative NuGet repository username password/apikey
  -uspct, --isBaseUrlPasswordClearText                                         Alternative NuGet repository password is cleartext
  -rs, --recursive                                                             To be used with a single project file, it will recursively scan project references of the supplied project file
  -ns, --no-serial-number                                                      Optionally omit the serial number from the resulting BOM
  -gu, --github-username <github-username>                                     Optionally provide a GitHub username for license resolution. If set you also need to provide a GitHub personal access token
  -gt, --github-token <github-token>                                           Optionally provide a GitHub personal access token for license resolution. If set you also need to provide a GitHub username
  -gbt, --github-bearer-token <github-bearer-token>                            Optionally provide a GitHub bearer token for license resolution. This is useful in GitHub actions
  -egl, --enable-github-licenses                                               Enables GitHub license resolution
  -dpr, --disable-package-restore                                              Optionally disable package restore
  -dhc, --disable-hash-computation                                             Optionally disable hash computation for packages
  -dct, --dotnet-command-timeout <dotnet-command-timeout>                      dotnet command timeout in milliseconds (primarily used for long dotnet restore operations) [default: 300000]
  -biop, --base-intermediate-output-path <base-intermediate-output-path>       Optionally provide a folder for customized build environment. Required if folder 'obj' is relocated.
  -imp, --import-metadata-path <import-metadata-path>                          Optionally provide a metadata template which has project specific details.
  -ipr, --include-project-references                                           Include project references as components (can only be used with project files).
  -sn, --set-name <set-name>                                                   Override the autogenerated BOM metadata component name.
  -sv, --set-version <set-version>                                             Override the default BOM metadata component version (defaults to 0.0.0).
  -st, --set-type   <Application|Container|Data|Device|Device_Driver|          Override the default BOM metadata component type (defaults to application). [default: Application]
                     File|Firmware|Framework|Library|
                     Machine_Learning_Model|Null|Operating_System|Platform>
  -ef, --exclude-filter <exclude-filter>                                       A comma separated list of dependencies to exclude in form 'name1@version1,name2@version2'. Transitive dependencies will also be removed.
  -F, --output-format <Auto|Json|UnsafeJson|Xml>                               Select the BOM output format: auto (default), xml, json, or unsafeJson (relaxed escaping). [default: Auto]                            
  --set-nuget-purl                                                             Override the default BOM metadata component bom ref and PURL as NuGet package.
  --version                                                                    Show version information
  -?, -h, --help                                                               Show help and usage information
```

*   `-ef, --exclude-filter`  
    The exclude filter may be used to exclude any packages, which are resolved by NuGet, but do not exist
    in the final binary output. For example, an application targets .NET 8, but has a dependency to a library,
    which only supports .NET Standard 1.6. Without filter, the libraries of .NET Standard 1.6 would be in the
    resulting SBOM. But they are not used by application as they do not exist in the binary output folder.

#### Examples
To run the **CycloneDX** tool you need to specify a solution or project file. In case you pass a solution, the tool will aggregate all the projects.

The following will create a BOM from a solution and all projects defined within:
```bash
dotnet-CycloneDX YourSolution.sln -o /output/path
```

The following will recursively scan the directory structure for packages.config and create a BOM:
```bash
dotnet-CycloneDX /path/to/project -o /output/path
```

The following will recursively scan the project references of the supplied project file, and create a BOM of all package references from all included projects:
```bash
dotnet-CycloneDX /path/to/project/MyProject.csproj -o /output/path -rs
```

The following will create a BOM from a project and exclude transitive dependency .NET Standard:
```bash
dotnet-CycloneDX /path/to/project/MyProject.csproj -o /output/path -ef NETStandard.Library@1.6.0
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

To build and test the solution locally you should have .NET 8.0 or .NET 9.0
installed. Standard commands like `dotnet build` and `dotnet test` work.

Alternatively, you can use VS Code and the included devcontainer configuration
to work in a pre-configured docker image. (You will also need the "Remote - Containers"
extension and Docker)

It is generally expected that pull requests will include relevant tests.
Tests are automatically run on Windows, MacOS and Linux for every pull request.
And build warnings will break the build.

If you are having trouble debugging a test that is failing for a platform you
don't have access to please let us know.

Thanks to [Gitpod](https://gitpod.io/) there is a really easy way of creating
a ready to go development environment with VS Code. You can open a Gitpod
hosted development environment in your browser.

[![Open in Gitpod](https://gitpod.io/button/open-in-gitpod.svg)](https://gitpod.io/#https://github.com/CycloneDX/cyclonedx-dotnet)
