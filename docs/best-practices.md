# Best Practices

## Point the tool at your root `.csproj`

The recommended input is the **root `.csproj`** of the application you are shipping. This
is typically your ASP.NET web application, Windows Forms application, console application,
or Worker Service project — the executable, deployable artifact.

When you run `dotnet restore` on a project, NuGet resolves the entire dependency graph —
including all transitive NuGet packages and everything pulled in through
`<ProjectReference>` elements — and writes the full closure into that project's
`obj/project.assets.json`. CycloneDX reads that single file and therefore already
produces a complete, accurate SBOM with no extra flags.

This means:

- **`--recursive` is not needed** to capture dependencies of referenced projects or their
  transitive packages. For modern SDK-style projects, it is effectively a no-op for the
  package list. Use it only if a referenced project still uses the old `packages.config`
  format (see [project-references.md](project-references.md)).
- **A `.sln` file is not a good input.** A solution is an arbitrary grouping of projects
  for IDE convenience — it has no defined root and no single dependency closure. Use the
  `.csproj` of the deployable artifact instead.

```sh
# Correct: point at the project you ship
dotnet-CycloneDX src/MyApp/MyApp.csproj --configuration Release

# Avoid: solution files produce an arbitrary union of all projects' dependencies
dotnet-CycloneDX MyApp.sln
```

---

## Restore first, then run the tool

The recommended way to run CycloneDX is to restore your project yourself and then pass
`--disable-package-restore` (`-dpr`). This gives you full control over the restore — the
configuration, any custom MSBuild properties, and the NuGet feed configuration — and
decouples the SBOM step from network access. It also makes it straightforward to run
CycloneDX in a container or air-gapped environment where access to private NuGet feeds may
not be available at SBOM-generation time.

```sh
dotnet restore src/MyApp/MyApp.csproj -p:Configuration=Release
dotnet-CycloneDX src/MyApp/MyApp.csproj --disable-package-restore
```

Always restore with the configuration you ship. `dotnet restore` passes the configuration
to MSBuild, which evaluates all `Condition` attributes before resolving the package graph.
This ensures that packages conditional on `$(Configuration)` — or any other MSBuild
property — are correctly included or excluded:

```xml
<ItemGroup>
  <PackageReference Include="MyApp.Core" Version="1.0.0" />
  <PackageReference Include="MyApp.Diagnostics" Version="1.0.0"
                    Condition="'$(Configuration)' == 'Debug'" />
</ItemGroup>
```

For conditions involving custom properties, pass those too:

```sh
dotnet restore src/MyApp/MyApp.csproj -p:Configuration=Release -p:UITestsEnabled=false
dotnet-CycloneDX src/MyApp/MyApp.csproj --disable-package-restore
```

### Target framework and runtime

If your project multi-targets and you want an SBOM for a specific target, pass
`--framework` and/or `--runtime`:

```sh
dotnet restore src/MyApp/MyApp.csproj -p:Configuration=Release
dotnet-CycloneDX src/MyApp/MyApp.csproj --disable-package-restore --framework net8.0 --runtime linux-x64
```

This ensures the resolved package graph matches the exact artifact you are shipping.

---

## Set BOM metadata

CycloneDX can populate the `<metadata>` block of the generated BOM — including the
component name, version, description, licenses, and purl. The recommended approach for
CI pipelines is to maintain a static metadata template with org-wide defaults and override
the version per build:

```sh
dotnet restore src/MyApp/MyApp.csproj -p:Configuration=Release
dotnet-CycloneDX src/MyApp/MyApp.csproj --disable-package-restore \
  --import-metadata-path ./metadata-template.xml \
  --set-version "$VERSION"
```

If your project file already contains `<Version>`, the tool picks it up automatically
without any extra flags.

See [bom-metadata.md](bom-metadata.md) for the full list of sources, precedence rules,
and template file format.
