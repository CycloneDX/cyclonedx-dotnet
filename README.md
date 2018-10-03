# dotnet-ossindex

A .NET Core global tool to list vulnerable Nuget packages.

The **dotnet-ossindex** checks the packages for known vulnerabilities using the [Sonatype OSS Index API](#sonatype-oss-index).

- [Installation](#installation)
- [Usage](#usage)
- [Sonatype OSS Index](#sonatype-oss-index)

## Installation

Download and install the [.NET Core 2.1 SDK](https://www.microsoft.com/net/download) or newer. Once installed, run the following command:

```bash
dotnet tool install --global dotnet-ossindex
```

If you already have a previous version of **dotnet-ossindex** installed, you can upgrade to the latest version using the following command:

```bash
dotnet tool update --global dotnet-ossindex
```

## Usage

```text
Usage: dotnet ossindex [arguments] [options]

Arguments:
  Path                        The path to a .sln, .csproj or .vbproj file

Options:
  -u|--username <USERNAME>    OSS Index Username
  -a|--api-token <API_TOKEN>  OSS Index API Token
  -?|-h|--help                Show help information
```

To run the **dotnet-ossindex** tool you need to specify a solution or project file. In case you pass a solution, the tool will automatically scan all the projects for vulnerabilities.

```bash
dotnet ossindex YourSolution.sln
```

![Screenshot of dotnet-ossindex](screenshot.png)

### OSS Index API rate limit

The OSS Index REST API has a rate limit for unauthenticated requests. If you exceed the limit, you can create an account on their [website](https://ossindex.sonatype.org) and use the `--username/--api-token` options to execute authenticated requests.

```bash
dotnet ossindex YourSolution.sln --username <YOUR-USERNAME> --api-token <YOUR-API-TOKEN>
```

# Sonatype OSS Index

OSS Index is a free service used by developers to identify open source dependencies and determine if there are any known, publicly disclosed, vulnerabilities. 

You can read more about the service here https://ossindex.sonatype.org.
