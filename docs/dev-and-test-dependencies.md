# Dev and Test Dependency Handling

This document explains how the CycloneDX .NET tool identifies and handles development
dependencies and test-project dependencies when scanning a project or solution.

---

## Two orthogonal concepts

The tool treats these as separate concerns:

| Concept | Granularity | Detection source | CLI flag |
|---|---|---|---|
| **Dev dependency** | Individual package | `project.assets.json` or `packages.config` | `--exclude-dev` |
| **Test project** | Entire project | `.csproj` content | `--exclude-test-projects` |

---

## Dev dependencies

### What counts as a dev dependency

A package is a dev dependency when the project explicitly marks it as private — meaning it
is consumed during development or build but must not be propagated to consumers of the
project.

**SDK-style projects (`PackageReference`)**

In the `.csproj` file, a package is marked private with:

```xml
<PackageReference Include="SomePackage">
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

The tool detects this by reading `project.assets.json` (via `NuGet.ProjectModel`) and
checking the `SuppressParent` field on each `LibraryDependency`. When `PrivateAssets=all`
is set, NuGet stores a non-default `SuppressParent` value in the assets file.

Detection logic — `ProjectAssetsFileService.cs:127–130`:

```csharp
public bool SetIsDevDependency(LibraryDependency dependency)
{
    return dependency != null
        && dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent;
}
```

**Legacy `packages.config` projects**

The `packages.config` format supports a `developmentDependency` XML attribute:

```xml
<package id="SomePackage" version="1.0.0" developmentDependency="true" />
```

Detection logic — `PackagesFileService.cs:66`:

```csharp
IsDevDependency = reader["developmentDependency"] == "true",
```

### Default behaviour (no flag)

Dev dependencies are **included** in the SBOM with `scope="required"`. The `IsDevDependency`
flag is tracked internally but does not change the output unless `--exclude-dev` is passed.

### With `--exclude-dev` / `-ed`

Dev dependencies are **removed from `components` and the dependency graph**, and instead
placed into `bom.formulation` as a single `Formula` entry. This keeps them in the SBOM
for supply chain transparency while correctly marking them as build-time inputs rather
than runtime components.

The flag first marks all detected dev dependencies as `Scope = Excluded`
(`Runner.cs:295–302`), then skips them when building the component list
(`Runner.cs:330–340`) and the dependency graph (`Runner.cs:347`), and finally
collects them into `bom.Formulation` after the BOM is assembled.

---

## Test projects

### What counts as a test project

The tool reads the project file (`.csproj`, `.fsproj`, etc.) and checks for either of these
signals (`ProjectFileService.cs:82–85`):

1. A `<PackageReference Include="Microsoft.NET.Test.Sdk">` element anywhere in an
   `<ItemGroup>`.
2. A `<PropertyGroup>` that contains `<IsTestProject>true</IsTestProject>`.

Either condition is sufficient.

### Default behaviour (no flag)

When scanning a solution, non-test projects are processed **before** test projects
(`SolutionFileService.cs:179`):

```csharp
var projectQuery = from p in projectPaths
                   orderby _projectFileService.IsTestProject(p)
                   select p;
```

This ordering ensures that if a package appears in both a production project and a test
project, the production project's entry wins in the merged set (where it carries
`scope="required"`). The test project's version of the same package would be a duplicate
and is not added.

For packages that appear **only** in test projects, every package from that project is
marked `Scope = Excluded` — set on each `DotnetDependency` as it is loaded
(`ProjectAssetsFileService.cs:85–89`):

```csharp
if (isTestProject)
{
    package.Scope = Component.ComponentScope.Excluded;
}
```

These packages end up in the SBOM with `scope="excluded"`.

### With `--exclude-test-projects` / `-t`

Test projects are **skipped entirely** — no packages are loaded from them at all
(`ProjectFileService.cs:183–187`):

```csharp
if (excludeTestProjects && isTestProject)
{
    Console.WriteLine($"Skipping: {projectFilePath}");
    return new HashSet<DotnetDependency>();
}
```

---

## Behaviour summary

| Situation | Default output | `--exclude-dev` | `--exclude-test-projects` |
|---|---|---|---|
| Package with `PrivateAssets=all` | Included, `scope="required"` | Moved to `formulation` (not in `components`) | (no effect) |
| Package in `packages.config` with `developmentDependency="true"` | Included, `scope="required"` | Moved to `formulation` (not in `components`) | (no effect) |
| Package only in a test project | Included, `scope="excluded"` | (no effect) | Omitted entirely |
| Package in both a production and a test project | Included once, `scope="required"` | (no effect) | Included, `scope="required"` |

---

## Manual exclusion

For cases not covered by the above, `--exclude-filter` / `-ef` accepts a comma-separated
list of packages to remove by name or `name@version`. Transitive dependencies that become
orphaned after removal are also pruned.

---

## CycloneDX `scope` field

### Spec definitions

The CycloneDX specification (v1.6) defines three values for the `scope` field on a component:

- **`required`** — the component is required for runtime. This is the assumed default when
  no scope is specified.
- **`optional`** — the component is optional at runtime. Reserved for components that are
  installed but not reachable because they are not configured or otherwise accessible; a
  component that is prohibited from being called by configuration must still be `required`.
- **`excluded`** — "components that are excluded provide the ability to document component
  usage for test and other non-runtime purposes. Excluded components are not reachable
  within a call graph at runtime."

### Steve Springett's stated intent for `excluded`

The written spec definition of `excluded` mentions "test" purposes, but Steve Springett
(the spec author) clarified the intended meaning in
[CycloneDX/specification#293 (comment)](https://github.com/CycloneDX/specification/issues/293#issuecomment-1869969390):

> "`scope` is a way to include a component but not assert that that component is delivered
> or included in the inventory. For example, you may want to include `Windows XP Embedded`
> as a component, mark the scope as excluded, as a way to indicate that the entire stack
> will include the OS, but that the supplier does not provide it with the software."

His example — an embedded OS that the supplier does not bundle — is about **externally
supplied or non-bundled components**, not test or dev dependencies. This is a narrower
and different concept from what the written definition implies.

This tension was noted in
[CycloneDX/specification#321](https://github.com/CycloneDX/specification/issues/321),
where a new scope value (`external` or `extraneous`) is being introduced in CycloneDX
v1.7 specifically for the non-bundled case Steve described. That leaves `excluded` with
genuinely ambiguous intent until the spec documentation is updated.

A maintainer of this repo confirmed the ambiguity in
[issue #843](https://github.com/CycloneDX/cyclonedx-dotnet/issues/843):

> "After checking with different people in the CycloneDX core group, I got different
> answers how to handle dev-dependencies and what e.g. excluded scope is meant for."

### How this tool uses `scope`

| Situation | Default `scope` | Assessment |
|---|---|---|
| Package only in a test project | `excluded` | Misuse per Steve's stated intent; aligns with the broader written spec text ("test and other non-runtime purposes") |
| Dev dependency (`PrivateAssets=all`), no flag | `required` | Defensible — see below |
| Dev dependency with `--exclude-dev` | omitted entirely | Acceptable |

**Dev dependencies and `scope="required"`**

The written spec definition of `required` is "the component is required for runtime."
Dev dependencies are not present at runtime, so `required` is not an accurate description
of their runtime role.

However, CycloneDX v1.4 introduced a top-level `formulation` construct for documenting
the build environment — compilers, tools, CI steps, and the packages used to produce the
artifact. Dev dependencies are legitimate inputs to *formulation*: a code generator, a
build-time analyzer, or a packing tool is genuinely required to build the software, even
if it is absent at runtime. This is where dev dependencies properly belong in the spec.

**Current behavior:** dev dependencies are emitted in `components` with `scope="required"`.
This is a reasonable approximation — they are required to produce the artifact — but it
conflates "required to build" with "required at runtime."

**With `--exclude-dev`:** dev dependencies are removed from `components` and the dependency
graph, and placed into the `formulation` section of the SBOM as a single `Formula` entry
whose `components` list contains all dev packages. This preserves supply chain transparency
and vulnerability tracking for build tools while correctly separating build-time inputs
from the runtime inventory.

The longer-term intended default is to emit dev dependencies in `formulation`
unconditionally, making `--exclude-dev` the flag that suppresses even that.

This is tracked in [issue #843](https://github.com/CycloneDX/cyclonedx-dotnet/issues/843)
with open PR [#844](https://github.com/CycloneDX/cyclonedx-dotnet/pull/844).

**Test-project packages and `scope="excluded"`**

This is a reasonable approximation under the written spec text but does not match Steve
Springett's stated intent for the field. The written spec text explicitly mentions "test
and other non-runtime purposes", so the current behaviour is not wrong in practice —
it just relies on the written definition rather than the author's intended design pattern.
