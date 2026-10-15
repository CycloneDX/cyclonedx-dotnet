# Dev and Test Dependency Handling

Development dependencies and test projects are separate concerns. Selected assets and
target-local dependency evidence determine development use; the project file determines
whether an entire project is a test project. `PrivateAssets="all"` alone does not identify
development-only use.
The existing `--exclude-dev` filter omits both development-only and test-project-only use.

## Development dependencies

For SDK-style `PackageReference` projects, the tool reads the restored
`project.assets.json` with `NuGet.ProjectModel`. Classification considers assets selected
for each target and the reference's selected asset categories (`IncludeAssets` and
`ExcludeAssets`), rather than every file shipped in the package.

- Selected analyzer, build, build-transitive, build-multitargeting or tool assets with no
  own selected runtime-capable assets initially identify development use. Build-only
  runtime aggregation wrappers can be refined to non-dev as described below.
- Compile, runtime, native, resource, runtime-target, framework, content and embedded
  assets are treated as runtime-capable. A package that also has these selected assets
  is not development-only.
- Explicitly selecting no assets (`ExcludeAssets="all"`), or only non-runtime asset
  categories, identifies development use too.
- An ambiguous package with no identified assets remains non-dev when runtime-capable
  categories are allowed. Absence of known runtime assets alone is not proof of build-only use.
- Analyzer detection combines selected asset flags with analyzer paths in the package
  manifest because the NuGet target model does not expose analyzer assets directly.

### Runtime aggregation wrappers

After classifying each package's own selected assets and resolving dependency ranges, the
tool refines build-only wrappers within each target (TFM/RID), before reachability and merging.
A wrapper with real selected build/build-transitive/build-multitargeting assets is non-dev
when its dependency closure has positive runtime-capable evidence matching the wrapper's
allowed categories, and the wrapper has no selected analyzer/tool evidence.

Evidence is a real asset path in the restored target inventory, grouped as Compile (including
framework and embedded assets), Runtime (including resources and non-native runtime targets),
Native (including native runtime targets), or ContentFiles. Empty paths and `_._` placeholders
do not count; neither does an assetless descendant's non-dev classification. A Native-only
selection cannot be justified by a managed Runtime asset. Explicitly selecting no assets or
only development categories is not overridden.

The search follows version/range-compatible dependencies in the same target, including nested
wrappers. Missing dependencies, incompatible versions and cycles without real evidence cannot
justify retention. Analyzer/tool occurrences with no runtime-capable inventory stop the evidence
search; mixed build-plus-analyzer/tool roots without own runtime evidence are not promoted.
These boundaries do not change ordinary runtime traversal from an existing non-dev root.
A descendant's separate direct-reference asset restriction is not applied globally to all
incoming paths: available evidence comes from its restored target inventory.

Thus a standalone build-only package without positive descendants stays development-only,
an explicitly build-only selection stays development-only, but an unrestricted build-only
wrapper over managed/native runtime packages can retain its whole closure in a production
project. Normal runtime sharing still wins over a separate development-only consumption path.

`PrivateAssets` controls propagation to consuming projects, not runtime use. For example:

```xml
<!-- A runtime library remains a runtime dependency even when private. -->
<PackageReference Include="RuntimeLibrary" Version="1.0.0" PrivateAssets="all" />

<!-- A mixed-asset package can be development-only when only build assets are selected. -->
<PackageReference Include="MixedPackage" Version="1.0.0" IncludeAssets="build" />

<!-- BuildTransitive-only parent with managed/native descendants: retained in production,
     even when private, provided the allowed categories match real descendant assets. -->
<PackageReference Include="RuntimeWrapper" Version="1.0.0" PrivateAssets="all" />

<!-- The same wrapper restricted to development categories is not promoted.
     Its exclusively development-used closure is omitted by --exclude-dev. -->
<PackageReference Include="RuntimeWrapper" Version="1.0.0" IncludeAssets="build;buildTransitive" />
```

The references above illustrate separate consumption choices, not simultaneous references
to the same package in one project. Package names are illustrative, not classification rules.

Restored dependency graphs do not encode whether an ordinary dependency serves application
runtime or implements a build tool. Conservative retention can therefore include ambiguous
build-tool closures with the same observable assets. `PrivateAssets="all"` alone does not
guarantee exclusion; this classification is not a publish-output scanner and does not interpret
MSBuild targets to infer deployment contents.

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

A project is recognized as a test project when MSBuild evaluates `IsTestProject` to `true`, or
its evaluated `PackageReference` items include `Microsoft.NET.Test.Sdk`, also when these are set
in an imported file such as `Directory.Build.props`. When the project cannot be evaluated, its
project XML is checked for either:

- a `PackageReference` to `Microsoft.NET.Test.Sdk`; or
- `<IsTestProject>true</IsTestProject>` in a property group.

By default, packages used only by test projects are included with `scope="excluded"`.
If the same package identity also has production runtime use, it is included once with
`scope="required"`. Test-project classification is independent of development-package
classification: a normal runtime library and its transitive dependencies in a test-only
project are not themselves development-only packages, but still have excluded scope and
are omitted by `--exclude-dev` unless also shared with production runtime use.
The same rule applies to promoted runtime aggregation wrappers: they are non-dev, but their
test-project-only closure still has excluded scope and is removed by `--exclude-dev`.

### `--exclude-test-projects` / `-t`

Test projects are skipped entirely. Their packages are not collected, but packages also
collected from production projects remain. Combine this option with `--exclude-dev` when
both test projects and development-only dependencies should be omitted.

## Behaviour summary

The production-package rows below assume no test-project-only use.

| Situation | Default output | `--exclude-dev` (deprecated) | `--exclude-test-projects` |
|---|---|---|---|
| Standalone build-only package without positive runtime-capable descendants, regardless of `PrivateAssets` | Included, `excluded` | Omitted | Unchanged |
| Explicitly build-only selection (mixed package or wrapper), with no separate production runtime path | Included, `excluded` | Omitted with exclusively development-used closure | Unchanged |
| Build-only runtime aggregation wrapper with matching descendant evidence and no selected analyzer/tool assets, regardless of `PrivateAssets` | Included, `required` with runtime closure | Retained, `required` with runtime closure | Unchanged |
| Analyzer/tool-only root, or mixed build-plus-analyzer/tool root without own runtime evidence, with no separate production runtime path | Included, `excluded` | Omitted with exclusively development-used closure | Unchanged |
| Runtime-capable package with `PrivateAssets="all"` | Included, `required` | Retained, `required` | Unchanged |
| `packages.config` package with `developmentDependency="true"` | Included, `excluded` | Omitted unless shared with production runtime use | Unchanged |
| Transitive package used exclusively through development roots | Included, `excluded` | Omitted | Unchanged |
| Transitive package shared with production runtime use | Included, `required` | Retained, `required` | Unchanged |
| Ordinary runtime package used only in test projects | Included, `excluded` | Omitted | Omitted |
| Promoted runtime aggregation wrapper and closure used only in test projects | Included, `excluded` | Omitted | Omitted |
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
graph and selected assets, not a binary call-graph analysis or a guarantee of publish contents.
The #1139 automated coverage uses an ONNX-shaped reduced assets fixture and synthetic
local-feed packages to verify restored inventories and CLI XML/JSON components and dependency
closure. It does not verify a real ONNX consumer's publish output.
