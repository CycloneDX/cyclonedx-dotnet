# License Resolution

How the tool resolves license information for NuGet packages, and what ends up in the BOM.
Phases run in order; the first to produce a result wins and the rest are skipped.

---

## NuGet license metadata

A nuspec can declare a license in several ways:

| Field | Notes |
|---|---|
| `<license type="expression">` | SPDX expression; current NuGet recommendation |
| `<license type="file">` | Path to a license file bundled inside the `.nupkg` |
| `<licenseUrl>` | Deprecated since 2019; still common in older packages |
| `<repository url="...">` | Source repository URL; may point to GitHub |
| `<projectUrl>` | Project homepage; may point to GitHub |

---

## Resolution phases

| # | Phase | When active |
|---|---|---|
| 1 | SPDX expression | always |
| 2 | GitHub license lookup | `--enable-github-licenses` |
| 3 | License file (embedded text) | `--include-license-text` |
| 4 | License URL fallback | always |

### Phase 1 — SPDX expression

Reads `<license type="expression">` from the nuspec. The expression is parsed into its leaf
identifiers; one `License{Id}` is emitted per leaf. No network calls. Most modern packages
use this.

### Phase 2 — GitHub license lookup (`--enable-github-licenses`)

Tries four URL sources against the GitHub API, in order:

1. `<licenseUrl>`
2. `<repository url>/blob/<commit>/licence` — only `master`/`main` refs (GitHub API limitation)
3. `<repository url>`
4. `<projectUrl>`

Non-GitHub URLs are skipped without a request. The API returns an SPDX ID where known;
`NOASSERTION` is mapped to `License{Name}`. Results are cached per URL for the run.

### Phase 3 — License file (`--include-license-text`)

Reads the file referenced by `<license type="file">` from the local NuGet cache and embeds
it as base64-encoded `AttachedText` in `License{Name, Text}`. Skipped if the package does
not use `<license type="file">` or the file is not found in the cache.

### Phase 4 — License URL fallback

Emits `License{Name="Unknown - See URL", Url=<licenseUrl>}` if `<licenseUrl>` is present.
If no URL is present either, no license node is emitted.

---

## Current behavior (pre phase 3)

Phase 3 does not exist yet. Packages using `<license type="file">` therefore fall through to
phase 4 with an empty `<licenseUrl>`, producing a useless stub entry — or no entry at all
when GitHub resolution is on. Affected packages include Lucene.Net, LibGit2Sharp, and
Oracle.ManagedDataAccess.Core.

The null-URL stub (phase 4 firing with no URL) is also a pre-existing bug that will be fixed
alongside phase 3.
