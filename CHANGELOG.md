# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [6.1.1] - 2026-04-08

### Fixed

- **Fix crash when a nuspec declares an exact-range version constraint across multiple projects** (#1071) — when a package's nuspec dependency uses an exact version range (e.g. `[1.0.0]`) and multiple versions of that package are present in a multi-project solution, the tool no longer crashes with "Unable to locate valid bom ref"; the dependency edge is resolved to the version that satisfies the range

## [6.1.0]

### Added

- **CycloneDX spec 1.7 support** — upgraded CycloneDX.Core from 11.0.0 to 12.0.1; generated BOMs now use the `bom/1.7` schema namespace
- **Allow credentials via environment variables** (#1036) — NuGet feed credentials can now be passed through environment variables
- **Allow exclude filter without version specifier** (#1014) — the `--exclude` filter no longer requires a version to be specified
- **Recursive scan warning** (#1037) — a warning is now emitted when scanning project references recursively to make the behavior more visible
- **End-to-end test suite** (#1032) — added E2E tests using Testcontainers and Verify snapshots

### Fixed

- **Fix project name resolution for classic .NET Framework projects** (#1051) — correctly resolve `AssemblyName` in projects using the default XML namespace
- **Fix case-insensitive comparison when pruning transitive deps** (#1025, #1040) — package names are now compared case-insensitively when removing unresolved transitive dependencies
- **Fix metadata import overrides** (#1041) — metadata values imported from project properties are no longer incorrectly overridden
- **Use `tools/components` instead of deprecated `tools/tool`** (#1043) — BOM metadata now uses the non-deprecated CycloneDX structure for recording tool information
- **Validate GitHub API redirect destination** (#1030) — redirect URLs from the GitHub API are now validated before following

### Security

- **Sanitize untrusted URL inputs from NuGet feed metadata** (#1033) — URLs from NuGet package metadata are now sanitized before use
- **Rootless container** (#1035) — Docker image now runs as a non-root user by default
- **Trusted publishing for .NET tool package** (#1054) — NuGet package publishing now uses trusted publishing

### Changed

- **Upgrade CycloneDX.Core from 10.0.1 to 12.0.1** (#1042) — via intermediate upgrade to 11.0.0; enables CycloneDX spec 1.7 output
- **Dependency updates**
  - actions/checkout: 6.0.1 → 6.0.2 (#1008, #1045)
  - actions/setup-dotnet: 5.0.1 → 5.2.0 (#1003, #1052)
  - actions/upload-artifact: 5.0.0 → 7.0.0 (#1031)

### Documentation

- Add security trust model (#1029)
- Move threat model and add architecture reference (#1034)
- Link NuGet and Docker Hub in README (#1019)
- Streamline README shields and links (#1018)
- Fix CI link in README (#1015)

## [6.0.0] - 2026-02-08

> **⚠️ WARNING: This is a MAJOR release with breaking changes.**
> 
> This release includes multiple significant changes that may affect compatibility:
> 
> 1. **Removed deprecated CLI arguments** - Several CLI flags have been removed. Scripts, CI/CD pipelines, and automation using these flags will break.
> 2. **Upgraded to .NET 10** - Runtime requirements have changed.
> 3. **Updated System.CommandLine** - Upgraded from beta4 to v2.0.0 final, which includes breaking API changes that may affect command-line behavior.
> 4. **Updated dependency versions** - NuGet packages, System.IO.Abstractions, and other dependencies have been upgraded.
> 
> **Action required:** Test thoroughly in a non-production environment before upgrading. Review all sections below for changes that may affect your use case.

### Breaking Changes

- **Remove deprecated CLI arguments** (#996, 0ae5d6a)
  - Removed `--json`/`-j` flag (replaced by `--output-format json`)
  - Removed `-f` flag (replaced by `-fn`/`--filename`)
  - Removed `-d` flag (replaced by `-ed`/`--exclude-dev`)
  - Removed `-r` flag (replaced by `-rs`/`--scan-project-references`)
  - Removed `--disable-github-licenses`/`-dgl` flag (already default behavior)
  - Removed `json` property from `RunOptions` model
  - Updated all tests to use `outputFormat` enum instead of boolean `json` flag
  - Cleaned up legacy flag handling logic in `Program.cs` and `Runner.cs`
  - **Note:** `--out` flag was restored before release for backward compatibility (see Fixed section below)

- **Upgraded System.CommandLine to v2.0.0** (#989, e11f8e7)
  - Upgraded from `2.0.0-beta4.22272.1` to `2.0.0` (stable release)
  - This version includes breaking API changes from the beta
  - Command-line parsing behavior may differ in edge cases

- **Minimum .NET runtime requirement** (#989, e11f8e7)
  - Now requires .NET 10 runtime (upgraded from .NET 9)
  - Docker images now use `mcr.microsoft.com/dotnet/sdk:10.0`

### Added

- **Documentation update** (#987, f041ac2)
  - Added `.slnx` format to supported file types in README

### Changed

- **Dockerfile improvements** (#993, edf2bd9)
  - Implemented multi-stage build (build + runtime stages) for smaller images
  - Changed from tool installation to direct publish deployment
  - Added environment variables for non-root execution: `DOTNET_CLI_HOME`, `NUGET_PACKAGES`
  - Made `/tmp/dotnet-home` and `/tmp/nuget-packages` writable for any user (chmod 0755)
  - Changed entrypoint from `CycloneDX` to `dotnet /app/CycloneDX.dll`
  - Fixed handling when no path argument is provided (now shows help instead of error)
  - Made `path` argument optional with `ArgumentArity.ZeroOrOne`

- **Upgrade to .NET 10** (#989, e11f8e7)
  - Updated target framework to `net10.0`
  - Updated SDK image to `mcr.microsoft.com/dotnet/sdk:10.0`
  - Updated System.IO.Abstractions from 21.0.2 to 22.1.0
  - Updated test runner packages (xunit.runner.visualstudio, coverlet.collector)
  - Fixed devcontainer Ubuntu 22.04 Dockerfile

- **Dependency updates**
  - actions/checkout: 5.0.0 → 6.0.1 (#986, #991)
  - actions/upload-artifact: 4.6.2 → 5.0.0 (#979)
  - actions/setup-dotnet: 5.0.0 → 5.0.1 (#988)
  - danielpalme/ReportGenerator-GitHub-Action (version bump) (#992)

### Fixed

- **Restore `--out` parameter for backward compatibility**
  - Reintroduced `--out` flag as a deprecated alias for `--output`/`-o` to maintain compatibility with existing GitHub Actions and CI/CD pipelines
  - The parameter is marked as deprecated with a message directing users to use `--output` instead
  - If both `--output` and `--out` are provided, `--output` takes precedence
  - Prevents breaking existing automation while encouraging migration to the new flag

- **Restore `--json` parameter for backward compatibility**
  - Reintroduced `--json` flag as a deprecated alias for `--output-format json` to maintain compatibility with existing GitHub Actions and CI/CD pipelines
  - The parameter is marked as deprecated with a message directing users to use `--output-format` instead
  - If `--json` is provided, it sets the output format to JSON
  - Prevents breaking existing automation while encouraging migration to the new flag

- **Missing using statement** (161766f)
  - Added missing `using System;` directive in Program.cs

### Security

- **Workflow security hardening** (#975, 39b8986)
  - Changed global `permissions: contents: read` to `permissions: read-all`
  - Follows principle of least privilege by limiting default permissions

- **Pin GitHub Actions versions** (1145c82)
  - Pinned all GitHub Actions to specific commit SHAs for reproducibility

- **Enable NuGet package locking** (#972, fad44df)
  - Added `packages.lock.json` files for both main and test projects
  - Enabled `RestorePackagesWithLockFile` in Directory.Build.props
  - Updated CI/CD workflows to use locked restore

- **Update NuGet dependencies** (#973, e930da1)
  - Bumped `NuGet.ProjectModel` from 6.9.1 to 6.14.0
  - Bumped `NuGet.Protocol` from 6.9.1 to 6.14.0

## [5.5.0] - 2025-10-06

### Changed

- Initial baseline for changelog tracking
