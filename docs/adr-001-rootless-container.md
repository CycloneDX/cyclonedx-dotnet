# ADR-001: Rootless Container Execution

**Date:** 2026-03-01  
**Status:** Partially implemented — deferred to next major version

## Context

Running Docker containers as root is a security weakness. If the container process is
compromised, the attacker has root inside the container, which increases the blast radius
of an escape or a volume-mounted file system attack.

The goal was to make the `cyclonedx/cyclonedx-dotnet` Docker image run as a non-root user
by default.

## Problem

The tool calls `dotnet restore` against the project files passed in via a volume mount. The
`dotnet` CLI writes intermediate build artefacts (the `obj/` directory) directly into the
project tree. This means the container user must have write access to the mounted volume.

When a caller runs:

```bash
docker run --rm -v $(pwd):/work cyclonedx/cyclonedx-dotnet /work/MyProject.sln -o /work
```

the volume is owned by the host UID. If the container runs as a different UID, `dotnet restore`
fails with `Permission denied` on the `obj/` directory.

## Options considered

### Option 1: Dedicated non-root user baked into the image (rejected — breaking)

Create a non-root user in the image and set `USER` to it. Callers who do not pass `--user`
would run as that UID, which does not match the host volume owner, causing `dotnet restore`
to fail with `Permission denied` on the `obj/` directory. This is a silent breaking change
for all existing pipelines regardless of which non-root UID is chosen.

### Option 2: Require `--user $(id -u):$(id -g)` (chosen)

Leave the image running as root by default to preserve backward compatibility. Document that
callers should pass `--user $(id -u):$(id -g)` to run as their own UID. This means the
container process matches the volume owner, so `dotnet restore` can write `obj/` without
permission errors, and output files are owned by the calling user.

This is not enforced by the image, but it is safe for all callers who adopt it, and it is
the standard pattern for Docker tools that write back to mounted volumes.

## Decision

**Defer the default non-root user to the next major version**, where the breaking change
can be communicated via release notes and a migration guide.

In the interim:

- The image continues to run as root by default.
- The `DOTNET_CLI_HOME` and `NUGET_PACKAGES` directories (`/tmp/dotnet-home` and
  `/tmp/nuget-packages`) are created with `chmod 1777` (world-writable, sticky bit) so
  they are accessible to any UID — including one injected at runtime via `--user`.
- Callers are recommended to pass `--user $(id -u):$(id -g)` (documented in the README).
- The release workflow smoke-test already uses `--user $(id -u):$(id -g)`.

## Consequences

- No breaking change for existing pipelines.
- Callers who adopt `--user $(id -u):$(id -g)` get rootless execution today.
- The next major version should set a non-root `USER` in the Dockerfile and update
  documentation accordingly, accepting the breaking change with a migration note.
