# CycloneDX .NET — Architecture and Behavior Reference

This document describes what the tool does, how it does it, and where the edge cases are.
It is intended for contributors and security reviewers, not end users.

---

## Overview

`dotnet-CycloneDX` is a CLI tool that produces a CycloneDX SBOM from a .NET solution or
project. It resolves the full transitive dependency graph, fetches package metadata from the
local NuGet cache (or the configured feed), optionally resolves SPDX license identifiers via
the GitHub API, and writes the result as XML or JSON.

It has no server component, no persistent state, and no interactive UI. It is designed to run
unattended in CI/CD pipelines.

---

## Execution Flow

```
CLI args (Program.cs)
  → RunOptions
  → Runner.HandleCommandAsync
      1. Resolve NuGet cache paths          (dotnet nuget locals ...)
      2. Init GitHub service (if enabled)
      3. Init NuGet service
      4. Collect dependencies               (see: Input Dispatch)
      5. Apply exclude filter + orphan removal
      6. Resolve components (nuspec lookup)
      7. Wire dependency graph
      8. Assemble and write BOM
```

---

## Input Dispatch

The positional `<path>` argument selects the collection strategy:

| Input | Strategy |
|---|---|
| `.sln` | Parse project list from solution file |
| `.slnx` | Parse project list from XML solution file |
| `.slnf` | Parse project list from solution filter JSON |
| `.*proj` + `--recursive` | BFS over `<ProjectReference>` elements |
| `.*proj` (no `--recursive`) | Single project only |
| `packages.config` | Parse packages.config directly |
| directory | Recursive scan for all `packages.config` files |

Supported project extensions: `.csproj`, `.fsproj`, `.vbproj`, `.xsproj`.

### Solution files

`.sln`: project paths extracted by regex from `Project(...)` lines; backslashes normalized.

`.slnf`: JSON with `solution.path` and `solution.projects[]`. Project paths are relative to the
`.sln` file referenced in the filter, not to the `.slnf` itself.

`.slnx`: XML; `<Project Path="...">` elements extracted by regex.

All three variants call `RecursivelyGetProjectReferencesAsync` on each listed project and
union-add discovered project references. Projects not in the solution file but referenced by a
listed project are included.

---

## Dependency Collection

### PackageReference projects

1. `dotnet restore` is run per project (unless `--disable-package-restore`).
   Timeout: 300 000 ms (override: `--dotnet-command-timeout`). Errors read from stdout.
2. `project.assets.json` is located:
   - `<--base-intermediate-output-path>/obj/<projectName>/project.assets.json`, or
   - `<projectDir>/obj/project.assets.json`, or
   - queried via `dotnet msbuild -getProperty:ProjectAssetsFile`.
3. All `LockFileTarget` entries (one per TFM × RID combination) are iterated and
   union-merged into a single package set. Equality is `(Name, Version)`.
4. `IsDirectReference` is determined per-TFM from `ProjectFileDependencyGroups`.
5. `IsDevDependency` is set when `SuppressParent != DefaultSuppressParent`
   (corresponds to `<PrivateAssets>all</PrivateAssets>` / `developmentDependency="true"`).
6. Version ranges in dependency dictionaries are resolved to concrete versions already
   present in the package set.
7. If `NETStandard.Library` appears in dependency dicts but not in the resolved set, all
   references to it are stripped (SDK-provided; not a real package in the output).

If `project.assets.json` yields zero packages, the tool falls back to `packages.config` in
the same directory.

### packages.config projects

XML parsed directly. No restore, no assets file, no transitive graph. All packages are
`Required` scope; `developmentDependency="true"` is read from the XML attribute.

### Multi-TFM aggregation

A project targeting multiple frameworks produces one `LockFileTarget` per framework.
All are merged by union. If the same package appears in multiple targets with different
transitive dependency sets, the first-encountered instance wins (HashSet semantics). This
is a known limitation tracked by issue #911.

---

## Exclude Filter

`--exclude-filter name1@version1,name2` removes packages and their orphaned transitive
dependencies.

**Step 1 — direct removal**: Each token is matched by name (all versions) or `name@version`
(exact). Matched packages are removed from the set.

**Step 2 — orphan removal**: BFS from all `IsDirectReference == true` packages through their
`Dependencies` dicts. Any package not reachable from a direct reference is removed.
Orphan names are printed to console.

A transitive dependency shared between an excluded package and a non-excluded package is
kept, because it remains reachable via the non-excluded path.

---

## NuGet Metadata Resolution

For each package in the set, `NugetV3Service.GetComponentAsync` runs:

1. **Cache lookup**: `<cachePath>/<name.lower>/<normalizedVersion>/<name.lower>.nuspec`.
   Version normalization strips trailing `.0` from 4-part versions (`1.2.3.0` → `1.2.3`).
2. **Feed fallback**: if not cached, fetches the `.nupkg` from the configured NuGet feed
   (default: nuget.org v3) and extracts the nuspec in memory.

Fields extracted:
- `Authors`, `Copyright`, `Description` (priority: Summary > Description > Title)
- `ProjectUrl` → external reference (Website)
- `RepositoryMetadata.Url` → external reference (VCS); SCP-style git URLs
  (`git@github.com:owner/repo`) are normalised to `https://github.com/owner/repo`.
- Hash: SHA-512 read from `.nupkg.sha512` sidecar, or computed from `.nupkg`
  (unless `--disable-hash-computation`).

**License extraction** (in order, first match wins):

1. SPDX expression in nuspec (`<license type="expression">`) — parsed as expression tree,
   one `LicenseChoice` per leaf identifier.
2. GitHub license resolution (if `--enable-github-licenses`) against `licenseUrl`,
   then `repositoryUrl/blob/<commit>/licence`, then `repositoryUrl`, then `projectUrl`.
3. Raw license URL with `Name = "Unknown - See URL"`.

---

## GitHub License Resolution

Enabled with `--enable-github-licenses`. Optionally authenticated with
`--github-username` + `--github-token` (Basic) or `--github-bearer-token` (Bearer).
Unauthenticated: 60 requests/hour. Rate-limit exceeded (`403`) is a fatal error — no retry.

**Recognized URL patterns:**

- `https://github.com/<owner>/<repo>/blob/<ref>/licen[cs]e[.-suffix]`
- `https://raw.github(usercontent)?.com/<owner>/<repo>/<ref>/licen[cs]e[.-suffix]`
- `https://github.com/<owner>/<repo>` (bare repo)
- `https://raw.github(usercontent)?.com/<owner>/<repo>`
- `git@github.com:<owner>/<repo>` (SSH)

`repositoryId` segments and `refSpec` are `Uri.EscapeDataString`-encoded before being
interpolated into the API URL (`https://api.github.com/repos/<id>/license[?ref=<ref>]`).

**refSpec filter**: only `master` and `main` are accepted. Any other ref returns `null`
(license omitted). This is a known GitHub API limitation.

**Redirect handling**: one level of `301` is followed. The redirect target must be HTTPS and
within `api.github.com`, `*.github.com`, or `*.githubusercontent.com`. Anything else throws
`GitHubLicenseResolutionException`.

**`.git` suffix**: a `404` on a URL ending in `.git` is retried once after stripping `.git`.

**Result caching**: `ConcurrentDictionary` keyed by URL — each URL is only fetched once per
run.

`NOASSERTION` SPDX ID is treated as unknown: `License.Id` is set to null, `License.Name`
is set to the human-readable name from the API response.

---

## Project References

### Default (no `--include-project-references`)

Project-type entries from the assets file are removed from the component list. Their
transitive package dependencies are promoted to `IsDirectReference = true` and remain in
the BOM. Project references are also removed from all `Dependencies` dicts of other packages
to keep the graph consistent.

### With `--include-project-references`

Project-type entries are resolved to `Component` objects (Type: Library) using metadata
read directly from the `.csproj`/`.fsproj`/`.vbproj` XML. Version is sourced from
`<Version>`, or `AssemblyVersion` in `AssemblyInfo.cs`, or `"1.0.0"` as fallback. These
components get a `BomRef` but no NuGet PURL. Only valid when the input is a project file.

---

## Dependency Graph Assembly

After all components are resolved:

1. Each `DotnetDependency.Dependencies` dict entry is looked up in `bomRefLookup`
   (keyed by `(name.lower, version.lower)`).
2. If exact lookup fails, a name-only search is attempted. If exactly one match exists, it
   is used. If zero or more than one, the tool exits with `UnableToLocateDependencyBomRef`.
3. Project references are removed or promoted (see above) before graph wiring.

---

## Metadata Template (`--import-metadata-path`)

The template must be a valid CycloneDX XML BOM. It is deserialized in full and replaces the
in-memory BOM before component and dependency data is added.

After replacement, `SetMetadataComponentIfNecessary` fills blank fields only:
- `Metadata.Component.Name` ← inferred from solution/project filename
- `Metadata.Component.Version` ← `--set-version` or `"0.0.0"`
- `Metadata.Component.Type` ← `Application` if currently `Null`

`--set-name`, `--set-version`, `--set-type` override the above regardless of template content.
`--set-nuget-purl` sets both `Purl` and `BomRef` of the metadata component to
`pkg:nuget/<name>@<version>`.

`Metadata.Timestamp` defaults to `DateTime.UtcNow` if not provided by the template.
A `Tool` entry for `CycloneDX module for .NET` is always injected into `Metadata.Tools`.

---

## Output Formats

| Format | Flag | Filename default | Notes |
|---|---|---|---|
| XML | (default / `--output-format Xml`) | `bom.xml` | Standard escaping |
| JSON | `--output-format Json` | `bom.json` | Standard escaping |
| UnsafeJson | `--output-format UnsafeJson` | `bom.json` | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` — non-ASCII and special chars not escaped |
| Auto | `--output-format Auto` | inferred | Filename extension determines format; defaults to XML |

`Auto` (default): if `--filename` ends in `.json` → JSON; ends in `.xml` → XML; otherwise XML.
The deprecated `--json` flag forces JSON and maps to the `Json` format.

`UnsafeJson` is useful when license texts or descriptions contain Unicode that would
otherwise be escaped. It widens the attack surface for downstream parsers if any field
contains attacker-controlled content from an untrusted NuGet feed.

---

## Known Limitations

| Issue | Description |
|---|---|
| #911 | Multi-TFM projects: transitive dep sets from different frameworks are merged by union; first-seen version wins if versions conflict. |
| GitHub refSpec | Only `master` and `main` refs are resolved for license lookup. All other refs return no license. Upstream GitHub API limitation. |
| Rate limiting | GitHub API rate-limit (`403`) is an immediate fatal error. Use `--github-bearer-token` in CI or `--enable-github-licenses` only when authenticated. |
| packages.config | No transitive graph available — only explicitly listed packages appear in the BOM. |
| `--recursive` + multi-TFM | Sub-projects are aggregated across all their TFMs independently; the parent's active TFM is not applied to sub-project resolution. |
