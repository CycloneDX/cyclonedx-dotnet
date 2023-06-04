# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

## [Unreleased]

### Added

- use central package management and update dependencies #672
- Adding .net 7 references to project #666
- GitHub Performance & Edge case improvements #625
- Additional Lookups For License #615

### Fixed

- use suppressParent to identify development dependencies #693
- Fix bug when nuget package has a multiple condition license expression #640
- Normalize version string when locating files in the local cache #674
- Fix dependency name in Error log #650
- GitHub rate limit error fix #626

### Changed

- Bump System.IO.Abstractions.TestingHelpers from 17.2.16 to 19.2.26 #702
- Bump System.IO.Abstractions from 19.2.16 to 19.2.26 #701
- Bump actions/checkout from 3.1.0 to 3.5.2 #680
- Bump Moq from 4.18.2 to 4.18.4 #645
- Update ReadMe.md

## [2.7.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.6.0...v2.7.0) 022-11-30

### Added

- Feature release

## [2.6.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.5.1...v2.6.0) 2022-11-23

### Changed

- Update semver.txt

## [2.5.1](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.5.0...v2.5.1) 2022-10-17

### Fixed

- Bugfix release - handling for package version ranges

## [2.5.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.4.1...v2.5.0) 2022-10-17

### Added

- Feature release - multiple quality of life improvements and bug fixes

## [2.4.1](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.3.0...v2.4.1) 2022-10-12

### Security

- Security release - resolve CVE-2022-41032

## [2.3.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.2.0...v2.3.0) 2021-11-11

### Added

- Feature release - add .NET 6 support

## [2.2.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.1.2...v2.2.0) 2021-11-7

### Added

- Feature release - add option to disable package restore

## [2.1.2](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.1.1...v2.1.2) 2021-10-10

### Fixed

- Bugfix release - add handling for packages with no dependencies

## [2.1.1](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.1.0...v2.1.1) 2021-10-7

### Fixed

- Bug fix release - fix dependency graph package name casing issue

## [2.1.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.0.1...v2.1.0) 2021-10-6

### Added

- Feature release - add dependency graph support

## [2.0.1](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v2.0.0...v2.0.1) 2021-9-23

### Fixed

- Bugfix release - resolve null component scope issue for packages.conf

## [2.0.0](https://github.com/CycloneDX/cyclonedx-dotnet/compare/v1.6.2...v2.0.0) 2021-9-20

### Fixed

- Bugfix release - resolve null component scope issue for packages.conf
