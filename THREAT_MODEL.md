# CycloneDX .NET — Security Trust Model

CycloneDX for .NET is a CLI tool that generates Software Bill-of-Materials (SBOM) documents
from .NET solutions and projects. It runs primarily in CI/CD pipelines and Docker containers.

This document defines what the tool is and is not responsible for from a security perspective.
Its purpose is to help users, operators, and security researchers determine whether a reported
issue is in scope for this tool.

**Any issue that requires the compromise of a trusted element listed below is out of scope.**

---

## Trusted Elements

1. **Host OS, filesystem, installed certificates, PATH, and environment variables.** If the
   host is compromised, no output from this tool can be considered trustworthy.

2. **The .NET runtime and `dotnet` SDK.** These are part of the host environment. This
   includes the output of `dotnet` subprocesses spawned by the tool — since both the runtime
   and the repository it operates on are trusted, so is their combined output.

3. **The repository being scanned** — all source files, project files, solution files,
   `packages.config`, `project.assets.json`, and any metadata template supplied via
   `--import-metadata-path`. The tool's purpose is to describe the composition of a
   repository, not to evaluate the security or trustworthiness of its dependencies.

   The tool runs `dotnet restore` by default; this can be skipped with
   `--disable-package-restore` if the operator has already done so. Either way, NuGet packages
   referenced by the project and any MSBuild targets they executed during restore are covered
   by this trust assumption. The operator is responsible for the packages they include.

4. **The operator invoking the tool.** CLI arguments, credentials, and configuration options
   are operator-controlled.

---

## Untrusted Elements

1. **All data received from external network connections.** This includes NuGet feed responses,
   `.nuspec` files read from the local package cache (which is a local copy of feed data), and
   GitHub API responses. For private NuGet feeds, credentials are sent to the configured URL;
   the configured remote location is trusted, the data it returns is not.

   Untrusted data must not reach the SBOM output without validation. Embedding external data
   verbatim into the SBOM is a vulnerability — regardless of output format.

   We recommend configuring private NuGet feeds to serve over HTTPS.

2. **The output streams and process argument list.** stdout, stderr, and CLI argument values
   are observable by CI log systems, process inspection tools, and log aggregators. The tool
   must never write credentials or secrets to these streams. Operators are recommended to
   avoid passing secrets as CLI argument values and to use environment variable expansion
   provided by their CI platform instead.

---

## In Scope — Examples

- A malformed `.nuspec` field from a NuGet feed or the local package cache causing the tool
  to crash or produce a corrupted SBOM.
- Unsanitized content from a `.nuspec` field — such as special characters, malformed URLs, or
  excessively long strings — being embedded verbatim into the SBOM output and breaking
  downstream parsers or compliance tooling. The `--output-format unsafeJson` mode increases
  this risk by relaxing output escaping.
- Credentials appearing in stdout, stderr, or log output.

---

## Out of Scope — Examples

- **PATH manipulation to substitute the `dotnet` binary.** Requires host-level write access.
- **Spoofing an HTTPS endpoint via a rogue or misissued certificate.** Requires either
  tampering with the host certificate store or a global CA-level breach — both out of scope.
- **Malicious project files or a crafted `project.assets.json`.** The repository is trusted;
  the operator is responsible for what they scan.
- **A NuGet package using MSBuild targets to hide from or misrepresent itself to the scan.**
  Restore is a precondition; the operator is responsible for the packages they include.
- **`dotnet` subprocess producing unexpected output from a well-formed repository.** Both the
  runtime and the repository are trusted elements.
