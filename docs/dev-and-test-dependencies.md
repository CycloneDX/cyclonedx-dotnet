# Dev and Test Dependency Handling

This document explains how the CycloneDX .NET tool identifies and handles development
dependencies and test-project dependencies when scanning a project or solution.

---

## Two orthogonal concepts

The tool treats these as separate concerns:

| Concept | Granularity | Detection source | CLI flag |
|---|---|---|---|
| **Dev dependency** | Individual package | `project.assets.json` or `packages.config` | `--exclude-dev` (deprecated, no effect) |
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
    return dependency != null && dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent;
}
```

**Legacy `packages.config` projects**

The `packages.config` format supports a `developmentDependency` XML attribute:

```xml
<package id="SomePackage" version="1.0.0" developmentDependency="true" />
```

Detection logic — `PackagesFileService.cs:62`:

```csharp
var isDevDependency = reader["developmentDependency"] == "true";
```

### Default behaviour

Dev dependencies are **always** included in the SBOM with `scope="excluded"`. No flag is
needed — the scope is set at load time based on the `IsDevDependency` detection described
above.

### `--exclude-dev` / `-ed` (deprecated)

This flag is deprecated and has no effect. It is retained for backward compatibility so
that existing invocations do not break; passing it emits a warning at runtime.

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
var projectQuery = from p in projectPaths orderby _projectFileService.IsTestProject(p) select p;
```

This ordering ensures that if a package appears in both a production project and a test
project, the production project's entry wins in the merged set (where it carries
`scope="required"`). The test project's version of the same package would be a duplicate
and is not added.

For packages that appear **only** in test projects, every package from that project is
marked `Scope = Excluded` — set on each `DotnetDependency` as it is loaded
(`ProjectAssetsFileService.cs:85–89`):

```csharp
if (isTestProject || package.IsDevDependency)
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

| Situation | Default output | `--exclude-dev` (deprecated) | `--exclude-test-projects` |
|---|---|---|---|
| Package with `PrivateAssets=all` | Included, `scope="excluded"` | (no effect) | (no effect) |
| Package in `packages.config` with `developmentDependency="true"` | Included, `scope="excluded"` | (no effect) | (no effect) |
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

### How this tool uses `scope`

| Situation | `scope` |
|---|---|
| Package only in a test project | `excluded` |
| Dev dependency (`PrivateAssets=all` or `developmentDependency="true"`) | `excluded` |

**Dev dependencies and `scope="excluded"`**

Dev dependencies are not present at runtime and are not reachable within a call graph at
runtime. `scope="excluded"` is the correct value for this case per the CycloneDX
specification, and is consistent with the approach taken by other CycloneDX ecosystem
tools (npm, Maven, Composer, Poetry).

**Test-project packages and `scope="excluded"`**

Packages that appear only in test projects are not reachable at runtime. `scope="excluded"`
accurately describes their role per the spec definition ("test and other non-runtime
purposes").
