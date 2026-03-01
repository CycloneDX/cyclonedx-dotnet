# BOM metadata: sources, precedence, and options

CycloneDX .NET populates the `<metadata>` block of the generated BOM from up to
four sources.  They are applied in priority order — highest first:

| Priority | Source | How to use it |
|---|---|---|
| 1 (highest) | CLI arguments | `--set-name`, `--set-version`, `--set-type` |
| 2 | Metadata template file | `--import-metadata-path <file.xml>` |
| 3 | Project file `<Version>` | Read automatically from `.csproj`/`.fsproj`/`.vbproj` |
| 4 (lowest) | Hardcoded defaults | Name from filename; version `0.0.0`; type `Application` |

---

## Source 1 — CLI arguments

These flags always win, regardless of what the metadata file says.

| Flag | Short | Default | Description |
|---|---|---|---|
| `--set-name` | `-sn` | _(filename)_ | Override the metadata component `<name>` |
| `--set-version` | `-sv` | `0.0.0` | Override the metadata component `<version>` |
| `--set-type` | `-st` | `Application` | Override the metadata component `type` attribute |
| `--set-nuget-purl` | — | off | Generate a NuGet `pkg:nuget/<name>@<version>` purl and bom-ref if they are not already set |

`--set-name`, `--set-version`, and `--set-type` override only the specific field
they target; every other field from the template file (description, licenses,
authors, etc.) is preserved unchanged.

---

## Source 2 — Metadata template file (`--import-metadata-path`)

Pass a CycloneDX BOM XML file containing a `<metadata>` block.  Only the
`<metadata>` element is read; the rest of the file (component list, serial
number, etc.) is ignored.

```bash
dotnet-CycloneDX MyProject.csproj -o ./output -imp metadata-template.xml
```

**Template file format**

The file must be a valid CycloneDX BOM XML document. The spec version in the
XML namespace is used only for deserialization and does not affect the output
spec version (the tool always writes the current spec version).

```xml
<?xml version="1.0" encoding="utf-8"?>
<bom xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
     xmlns:xsd="http://www.w3.org/2001/XMLSchema"
     xmlns="http://cyclonedx.org/schema/bom/1.6">
  <metadata>
    <component type="application" bom-ref="pkg:nuget/MyApp@1.0.0">
      <name>MyApp</name>
      <version>1.0.0</version>
      <description>My application description.</description>
      <licenses>
        <license>
          <name>Apache License 2.0</name>
          <id>Apache-2.0</id>
        </license>
      </licenses>
      <purl>pkg:nuget/MyApp@1.0.0</purl>
    </component>
  </metadata>
</bom>
```

**What `<metadata>` can contain**

Everything the CycloneDX `metadata` type supports: `<component>`,
`<tools>`, `<authors>`, `<manufacture>`, `<supplier>`, `<licenses>`,
`<properties>`, and `<timestamp>`.

Two fields are always managed automatically by the tool:

- **`<timestamp>`** — if omitted or null, set to `DateTime.UtcNow` at BOM
  generation time.
- **`<tools>`** — the "CycloneDX module for .NET" tool entry is always inserted
  (or its version updated if it is already present).

---

## Source 3 — Project file version (automatic, `.csproj` only)

When scanning a `.csproj`, `.fsproj`, or `.vbproj` file and `--set-version` is
not provided, the tool reads the version from the project file and uses it as
the metadata component version.  It checks the following XML properties in
priority order, using the first one that has a value:

| Priority | Property | Typical use |
|---|---|---|
| 1 | `<Version>` | Standard SDK-style version |
| 2 | `<AssemblyVersion>` | Assembly-level version (may include revision) |
| 3 | `<ProjectVersion>` | Custom MSBuild property |
| 4 | `<PackageVersion>` | NuGet package version (may differ from assembly) |

If none of these properties is present in the project file, the tool falls back
to checking the legacy `AssemblyInfo.cs` / `AssemblyInfo.vb` / `AssemblyInfo.prg`
file for an `[assembly: AssemblyVersion(...)]` attribute.  If that is also
absent, the final fallback is `0.0.0`.

---

## Source 4 — Hardcoded defaults (automatic fallback)

When no template file is given and no `--set-*` flags are used, and the project
file contains no `<Version>`:

- **Name** is derived from the scanned file name without its extension
  (e.g. `MySolution` from `MySolution.sln`).
- **Version** defaults to `0.0.0`.
- **Type** defaults to `Application`.

---

## Precedence in detail

When both a template file and CLI arguments are provided, the merge rules are:

| Field | CLI arg provided? | Template has value? | Project file has value? | Result |
|---|---|---|---|---|
| name | yes | any | any | CLI arg wins |
| name | no | yes | any | template value kept |
| name | no | no/empty | any | derived from project filename |
| version | yes | any | any | CLI arg wins |
| version | no | yes | any | template value kept |
| version | no | no/empty | yes (csproj) | project file `<Version>` |
| version | no | no/empty | no | `0.0.0` |
| type | yes (non-default) | any | any | CLI arg wins |
| type | no | yes | any | template value kept |
| type | no | no/null | any | `Application` |

All other metadata fields (description, licenses, authors, purl, bom-ref, etc.)
are always taken entirely from the template file; there are no CLI args to
override them individually.

---

## Common usage patterns

**Let the project file supply the version automatically**

```bash
dotnet-CycloneDX MyLib.csproj -o ./bom
```

If `MyLib.csproj` contains `<Version>1.2.3</Version>`, the BOM metadata
component version will be `1.2.3` without any extra flags.

**Pin the version in CI without a template file**

```bash
dotnet-CycloneDX MySolution.sln -o ./bom -sv "$VERSION"
```

**Use a static template for org-wide defaults, override version per build**

```bash
dotnet-CycloneDX MySolution.sln -o ./bom \
  -imp ./company-metadata-template.xml \
  -sv "$VERSION"
```

All fields from the template (description, licenses, purl, etc.) are preserved;
only `<version>` is overridden by `$VERSION`.

**Set a NuGet-style purl when the package is published to NuGet**

```bash
dotnet-CycloneDX MyLib.csproj -o ./bom \
  -sn "MyLib" -sv "2.0.0" --set-nuget-purl
```

This sets `bom-ref` and `purl` to `pkg:nuget/MyLib@2.0.0`.
