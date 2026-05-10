# AGENTS.md

`dotnet-CycloneDX` is a .NET global CLI tool that generates CycloneDX SBOMs for .NET projects. It reads NuGet `project.assets.json` files (and optionally queries the NuGet API) to resolve dependencies, then uses the `CycloneDX.Core` library to produce the BOM in XML or JSON. See `docs/architecture.md` for a full behavioral reference.

## Projects

- `CycloneDX/` — CLI tool, `net8.0;net9.0;net10.0`
- `CycloneDX.Tests/` — xunit 2.x, `net8.0;net9.0;net10.0`
- `CycloneDX.E2ETests/` — xunit v3, `net10.0` only, requires Docker

## Build

```bash
dotnet clean && dotnet restore --locked-mode && dotnet build /WarnAsError
```

`dotnet clean` is required first — stale artifacts cause spurious warnings. CI fails on any warning.

## Test

```bash
dotnet test CycloneDX.Tests --framework net10.0
dotnet test CycloneDX.Tests --filter "FullyQualifiedName~ClassName.MethodName"
dotnet test CycloneDX.E2ETests --framework net10.0   # requires Docker
```

Update E2E snapshots (`CycloneDX.E2ETests/Snapshots/`):
```bash
VERIFY_AUTO_APPROVE=true dotnet test CycloneDX.E2ETests --framework net10.0
```

## Packages

Versions go in `Directory.Packages.props` only (Central Package Version Management). After any package change, regenerate and commit lock files:
```bash
dotnet restore   # no --locked-mode
```

## Gotchas

- `Directory.Build.props` defines `Windows`/`OSX`/`Linux` as compile-time constants.
- `CycloneDX.E2ETests.csproj` explicitly sets `<PackAsTool>false</PackAsTool>` to override the inherited default.
- Release version: edit `semver.txt`, then trigger `release.yml` (`workflow_dispatch`).

## Commits

Commits follow [Conventional Commits](https://www.conventionalcommits.org/). Commits require a `Signed-off-by` trailer. `CHANGELOG.md` is maintained manually — update it with user-facing changes.
