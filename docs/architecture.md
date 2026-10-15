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

## Project Evaluation

Project properties are evaluated by MSBuild, so values from imports such as
`Directory.Build.props`, property references and conditions are resolved. Every project found so
far (the projects of a solution, project references of all levels, which are read from the
project files first) is evaluated in the same `dotnet msbuild` call, using the .NET SDK the
dotnet CLI resolves for the working directory
(honoring `global.json`; .NET 8 SDK or newer). A generated helper project calls a target in each
project that returns the evaluated `Version`, `PackageId`, `AssemblyName`, `IsTestProject` and
`ProjectAssetsFile` properties and the `PackageReference` items. The target is added through the
SDK hooks `CustomAfterMicrosoftCommonTargets` and `CustomAfterMicrosoftCommonCrossTargetingTargets`.
A project that sets one of these properties itself is evaluated without its own file.
`--configuration` and `--framework` are passed as the global properties `Configuration` and
`TargetFramework`, like to `dotnet restore`.

A project that cannot be evaluated (e.g. a missing import, or an older SDK) does not affect the
others. Its values are read from the project file XML instead, and a notice with the MSBuild
error is printed. Project references are always read from the project file XML.

---

## Dependency Collection

### PackageReference projects

1. `dotnet restore` is run per project (unless `--disable-package-restore`).
   Timeout: 300 000 ms (override: `--dotnet-command-timeout`). Errors read from stdout.
2. `project.assets.json` is located:
   - `<--base-intermediate-output-path>/obj/<projectName>/project.assets.json`, or
   - `<projectDir>/obj/project.assets.json`, or
   - the evaluated `ProjectAssetsFile` property (see [Project Evaluation](#project-evaluation)), or
   - queried via `dotnet msbuild -getProperty:ProjectAssetsFile` when the project could not be
     evaluated.
3. All `LockFileTarget` entries (one per TFM × RID combination) are iterated and
   union-merged into a single package set. Equality is `(Name, Version)`.
4. `IsDirectReference` is determined per-TFM from `ProjectFileDependencyGroups`.
5. Initial `IsDevDependency` reflects each package's own assets selected by restore, not
   `PrivateAssets` or `SuppressParent` alone. Analyzer, build, build-transitive,
   build-multitargeting and tool assets without selected runtime-capable assets identify
   development use. Explicitly
   selecting no assets, or selecting only non-runtime asset categories, also identifies
   development use. Compile, runtime, native, resource, runtime-target, framework, content
   and embedded assets are runtime-capable; ambiguous asset-less packages remain non-dev
   when runtime-capable categories are allowed. Analyzer detection combines selected asset
   flags with analyzer paths in the package manifest.
6. Version ranges in dependency dictionaries are resolved to concrete versions already
   present in the target's package set.
7. If `NETStandard.Library` appears in dependency dicts but not in the resolved set, all
   references to it are stripped (SDK-provided; not a real package in the output).

After own-selected-assets classification and dependency range resolution, a graph refinement
runs within each `LockFileTarget`, before runtime reachability and `DotnetDependency.MergeInto`.
It changes eligible build-only wrapper occurrences to `IsDevDependency=false` when their
target-local dependency closure supplies positive evidence in an allowed runtime-capable
category. This retains runtime aggregation wrappers that carry only build/build-transitive/
build-multitargeting assets themselves, without making every build-only package non-dev.

- Candidates must have real selected build assets, allow runtime-capable categories, and
  have no selected analyzer/tool evidence. Standalone build-only packages without positive
  descendants, explicitly no-assets or development-only selections, and mixed build-plus-
  analyzer/tool candidates without their own runtime evidence remain development use.
- Positive evidence comes from the restored target-library inventory: **Compile** (including
  framework assemblies/references and embedded assets), **Runtime** (including resources and
  non-native runtime targets), **Native** (including native runtime targets), and **ContentFiles**.
  Empty paths, `_._` placeholders, unknown assets, and merely being classified non-dev are not
  positive evidence. The candidate's allowed-category mask must intersect this evidence;
  allowing Native alone cannot be justified by managed Runtime assets.
- Evidence follows case-insensitive, version/range-compatible dependency edges only within
  the current target. Missing or unsatisfied edges do not use the ordinary reachability pass's
  single-name fallback. A descendant's separate direct-reference selection is not a global
  restriction on other incoming paths; its restored target inventory supplies the available
  evidence. No edge-selection information absent from the assets file is inferred.
- Analyzer/tool occurrences with no runtime-capable inventory are boundaries in the evidence
  search. These boundaries do not alter ordinary forward runtime reachability. A reverse-edge
  worklist propagates category evidence through nested wrappers and cycles; cycles without
  real evidence cannot promote themselves. Evidence is never borrowed from merged targets.

Reachability is calculated within each selected target before merging. Traversal from direct,
non-dev references identifies non-development use, including ordinary dependencies of test
projects. Production runtime reachability excludes test-project roots. Packages reachable
only through dev/test paths have scope `Excluded`; production-runtime-shared packages have
scope `Required`. Merging the same `(Name, Version)` preserves reachability and dependency
edges from all occurrences rather than depending on encounter order.
Promoted wrappers in test projects gain non-development reachability, not production runtime
reachability: their test-only closure remains `Excluded` unless shared with production runtime use.
Unresolved edges originating in a production runtime path are resolved against the merged
package set. Their matching packages and descendants gain runtime reachability even when
those identities were supplied only by development occurrences in another target.

`--exclude-dev` is deprecated but still filters: it emits a warning on stdout and removes
development-only roots and their exclusively development-used transitives, as well as
ordinary runtime packages used only in test projects. Packages shared with production
runtime use remain `Required` and are retained. `--exclude-test-projects` skips test
projects entirely during collection. See
[Dev and Test Dependency Handling](dev-and-test-dependencies.md) for examples.

This is conservative classification of restored assets and dependency graphs, not a
publish-output scanner or an interpretation of MSBuild targets. Ordinary dependency edges
do not encode application-runtime versus build-tool intent, so indistinguishable build-tool
closures may also be retained as `Required`. `PrivateAssets` controls propagation to consumers,
not current-project intent, and does not guarantee exclusion. The #1139 regressions cover an
ONNX-shaped reduced assets fixture and synthetic local-feed restore/CLI XML and JSON closure;
they do not establish real ONNX application publish contents.

If `project.assets.json` yields zero packages, the tool falls back to `packages.config` in
the same directory.

### packages.config projects

XML parsed directly. No restore, no assets file, no transitive graph.
`developmentDependency="true"` is read from the XML attribute and those packages are assigned
`Excluded` during the runtime-scope pass; other packages are `Required`.

### Multi-TFM aggregation

A project targeting multiple frameworks produces one `LockFileTarget` per framework.
All are merged by package identity `(Name, Version)`, preserving dependency edges and
reachability from each target. Runtime use in any selected target keeps a shared package
`Required` and prevents `--exclude-dev` from removing it.

---

## Exclude Filter

`--exclude-filter name1@version1,name2` removes packages and their orphaned transitive
dependencies.

**Step 1 — direct removal**: Each token is matched by name (all versions) or `name@version`
(exact). Matched packages are removed from the set.

**Step 2 — orphan removal**: BFS from all `IsDirectReference == true` packages through their
`Dependencies` dicts, run once before step 1 and once after. Only packages that step 1 made
unreachable are removed, and their names are printed to console. Packages that were already
unreachable before the filter ran are kept, and their count is reported.

A transitive dependency shared between an excluded package and a non-excluded package is
kept, because it remains reachable via the non-excluded path.

Keeping the pre-existing orphans is deliberate. A package with no incoming edge is not
necessarily unused: edges are lost whenever packages from several targets or projects are
merged (see the union note above), so removing everything unreachable also removed genuinely
shipped packages.

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
evaluated by MSBuild (see [Project Evaluation](#project-evaluation)): the name is `PackageId`
(falling back to `AssemblyName`), matching the names NuGet writes to `project.assets.json`, and
the version is `Version`. If the evaluation fails or yields the SDK default version `1.0.0`, the
`.csproj`/`.fsproj`/`.vbproj` XML is read instead: version is sourced from `<Version>`, or
`AssemblyVersion` in `AssemblyInfo.cs`, or `"1.0.0"` as fallback. These
components normally get a `BomRef` of `<name>@<version>` but no NuGet PURL.
Only valid when the input is a project file.

When both `--include-project-references` and `--set-nuget-purl` are enabled,
each non-null project component gets a generated `pkg:nuget/<name>@<version>` PURL
only if its PURL is null or empty. Existing nonempty PURLs are preserved.
If a PURL is available, it replaces only a null/empty `BomRef` or the default
`<name>@<version>` reference; custom references are preserved. If generation
returns null, the original reference remains unchanged. This happens before
`bomRefLookup` is populated so dependency edges use the final reference.
Neither flag alone changes project identifiers, and NuGet package behavior is unchanged.

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
`--set-nuget-purl` generates `pkg:nuget/<name>@<version>` only when the metadata
component's `Purl` is null or empty, and uses that PURL as `BomRef` only when
`BomRef` is null or empty. Existing metadata PURLs and references (including
`<name>@<version>`) are preserved. The default-project-reference replacement
rule above does not apply to the root metadata component.

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
| GitHub refSpec | Only `master` and `main` refs are resolved for license lookup. All other refs return no license. Upstream GitHub API limitation. |
| Rate limiting | GitHub API rate-limit (`403`) is an immediate fatal error. Use `--github-bearer-token` in CI or `--enable-github-licenses` only when authenticated. |
| packages.config | No transitive graph available — only explicitly listed packages appear in the BOM. |
| `--recursive` + multi-TFM | Sub-projects are aggregated across all their TFMs independently; the parent's active TFM is not applied to sub-project resolution. |
