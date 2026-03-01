# Project references, `--recursive`, and `--include-project-references`

This document explains how CycloneDX .NET handles `<ProjectReference>` elements,
what `--recursive` (`-rs`) and `--include-project-references` (`-ipr`) actually do,
and when (if ever) you need them.

---

## How NuGet already handles project references

When you run `dotnet restore` on a project, NuGet writes a single
`obj/project.assets.json` file for that project. This file contains **every**
NuGet package in the full dependency closure — including packages that come
transitively through `<ProjectReference>` elements — because MSBuild resolves
the entire graph at restore time.

As a result, running CycloneDX against a `.csproj` **without any extra flags**
already produces a complete BOM of all NuGet packages, regardless of how deep
the `<ProjectReference>` tree goes. The `--recursive` flag is **not required**
to capture those packages.

This is confirmed by the `ProjectReferenceWithPackageReferenceWithTransitivePackage`
functional test: a root project that has only a `<ProjectReference>` (no direct
`<PackageReference>` entries) still yields a BOM containing all three NuGet
packages pulled in by the referenced project — with no flags set.

---

## What `--recursive` (`-rs`) actually does

`--recursive` causes the tool to walk the `<ProjectReference>` graph in the
`.csproj` XML and read each referenced project's **own** `project.assets.json`
separately, then merge the results.

For modern SDK-style projects (i.e. all references use `PackageReference`) this
produces **the same NuGet package set** as the non-recursive path, because the
root assets file already contains everything. The flag is effectively a no-op
for the package list in this case.

`--recursive` does make a real difference in two specific scenarios:

### 1. A referenced project uses `packages.config`

Old-style projects that use `packages.config` instead of `PackageReference` do
not participate in NuGet's lock-file resolution. Their packages are therefore
**absent from the root's `project.assets.json`**. The recursive scan discovers
those projects individually and reads their `packages.config` directly.

The `FrameworkPackagesConfigRecursive` functional test demonstrates this: the
non-recursive run produces 7 components; the recursive run produces 8, with
`log4net` (from a `packages.config`-based referenced project) being the extra
package.

### 2. You also want project nodes as BOM components (`--include-project-references`)

`--recursive` is a prerequisite for `--include-project-references`. Without it,
there are no project-reference nodes in the working set for `-ipr` to promote
into BOM components. See the next section.

---

## What `--include-project-references` (`-ipr`) does

This flag controls whether the **referenced projects themselves** appear as
components in the BOM, not whether their NuGet packages are captured (they are
captured either way, as described above).

Without `-ipr`, project reference nodes are stripped from the working set after
collection and their NuGet dependencies are promoted to direct dependencies of
the root. The BOM contains only NuGet package components.

With `-ipr`, each referenced project (e.g. `MyLib`) appears as a `library`
component in the BOM, and the full multi-level dependency graph is preserved:

```
MyApp → MyLib → SomeNuGetPackage
```

`-ipr` is only valid with a project file input. Passing it with a `.sln`,
directory, or `packages.config` path is a hard error.

---

## Decision guide

| Your situation | Recommended flags |
|---|---|
| Modern SDK-style project, you want all NuGet packages | _(none needed)_ |
| Referenced project uses `packages.config` | `--recursive` |
| You want referenced projects listed as BOM components | `--recursive --include-project-references` |
| Solution file input | _(none; solution path already aggregates all projects) _ |

---

## Summary

- The root `project.assets.json` already captures all NuGet packages from the
  full `<ProjectReference>` closure for SDK-style projects. No flags needed.
- `--recursive` is only meaningful when a referenced project uses `packages.config`,
  or when combined with `--include-project-references`.
- `--include-project-references` controls whether project nodes appear as BOM
  components. It does not affect which NuGet packages are included.
