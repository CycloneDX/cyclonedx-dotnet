# Dev and Test Dependency Handling

Development dependencies and test projects are separate concerns. A package's selected
assets determine development use; the project file determines whether an entire project
is a test project. `PrivateAssets="all"` alone does not identify development-only use.
The existing `--exclude-dev` filter omits both development-only and test-project-only use.

## Development dependencies

For SDK-style `PackageReference` projects, the tool reads the restored
`project.assets.json` with `NuGet.ProjectModel`. Classification considers assets selected
for each target and the reference's selected asset categories (`IncludeAssets` and
`ExcludeAssets`), rather than every file shipped in the package.

- Selected analyzer, build, build-transitive, build-multitargeting or tool assets with no
  selected runtime-capable assets identify development use.
- Compile, runtime, native, resource, runtime-target, framework, content and embedded
  assets are treated as runtime-capable. A package that also has these selected assets
  is not development-only.
- Explicitly selecting no assets (`ExcludeAssets="all"`), or only non-runtime asset
  categories, identifies development use too.
- An ambiguous package with no identified assets remains non-dev when runtime-capable
  categories are allowed. Absence of known runtime assets alone is not proof of build-only use.
- Analyzer detection combines selected asset flags with analyzer paths in the package
  manifest because the NuGet target model does not expose analyzer assets directly.

`PrivateAssets` controls propagation to consuming projects, not runtime use. For example:

```xml
<!-- A runtime library remains a runtime dependency even when private. -->
<PackageReference Include="RuntimeLibrary" Version="1.0.0" PrivateAssets="all" />

<!-- A mixed-asset package can be development-only when only build assets are selected. -->
<PackageReference Include="MixedPackage" Version="1.0.0" IncludeAssets="build" />
```

Legacy `packages.config` projects use the explicit XML attribute:

```xml
<package id="BuildTool" version="1.0.0" developmentDependency="true" />
```

These files provide no transitive dependency graph; each listed non-dev package is
treated as a non-development root.

### Default output

Development-only roots and packages reachable exclusively through development paths are
included with CycloneDX `scope="excluded"`. A transitive dependency also reachable from a
production runtime root remains `scope="required"`. Package identity is name and version;
shared use and dependency edges are merged across selected targets and projects, so the
result does not depend on which occurrence is encountered first.

### `--exclude-dev` / `-ed` (deprecated, still filters)

This option emits a warning on **stdout** stating that it is deprecated and still filters.
It removes development-only roots and their exclusively development-used transitives,
as well as ordinary runtime packages used only in test projects. Packages shared with
production runtime use are retained. Removed components and their
dependency references are omitted from the BOM.

This preserves the existing filtering behaviour for test-project-only packages, even
when their selected assets are runtime-capable. Deprecation does not make the option a no-op.

## Test projects

A project is recognized as a test project when its project XML contains either:

- a `PackageReference` to `Microsoft.NET.Test.Sdk`; or
- `<IsTestProject>true</IsTestProject>` in a property group.

By default, packages used only by test projects are included with `scope="excluded"`.
If the same package identity also has production runtime use, it is included once with
`scope="required"`. Test-project classification is independent of development-package
classification: a normal runtime library and its transitive dependencies in a test-only
project are not themselves development-only packages, but still have excluded scope and
are omitted by `--exclude-dev` unless also shared with production runtime use.

### `--exclude-test-projects` / `-t`

Test projects are skipped entirely. Their packages are not collected, but packages also
collected from production projects remain. Combine this option with `--exclude-dev` when
both test projects and development-only dependencies should be omitted.

## Behaviour summary

The production-package rows below assume no test-project-only use.

| Situation | Default output | `--exclude-dev` (deprecated) | `--exclude-test-projects` |
|---|---|---|---|
| Build-only package, regardless of `PrivateAssets` | Included, `excluded` | Omitted | Unchanged |
| Mixed package with only build assets selected | Included, `excluded` | Omitted | Unchanged |
| Runtime-capable package with `PrivateAssets="all"` | Included, `required` | Retained, `required` | Unchanged |
| `packages.config` package with `developmentDependency="true"` | Included, `excluded` | Omitted unless shared with production runtime use | Unchanged |
| Transitive package used exclusively through development roots | Included, `excluded` | Omitted | Unchanged |
| Transitive package shared with production runtime use | Included, `required` | Retained, `required` | Unchanged |
| Ordinary runtime package used only in test projects | Included, `excluded` | Omitted | Omitted |
| Development-only package used only in test projects | Included, `excluded` | Omitted | Omitted |
| Package shared with production runtime and test use | Included once, `required` | Retained, `required` | Retained, `required` |

## Manual exclusion

`--exclude-filter` / `-ef` accepts a comma-separated list of package names or
`name@version` identities to remove. Transitive dependencies that become orphaned after
removal are pruned. This explicit filter is separate from development and test classification.

## CycloneDX scope

`required` describes runtime use; `excluded` documents test and other non-runtime use.
Excluded scope does **not** mean a component is absent from the BOM. By default the tool
documents development and test dependencies; the exclusion options control whether those
components are collected or emitted. These classifications describe the restored dependency
graph and selected assets, not a binary call-graph analysis.
