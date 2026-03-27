# Build and Release Plan

## Problem Statement

The project currently has no CI/CD pipeline beyond the documentation workflow. We need GitHub Actions workflows that:

1. Run builds and tests on every PR and push to `main`.
2. On a version tag push (`v*.*.*`), publish release artifacts for the **Server** (`Sannel.Encoding.Manager.Web`) and **Runner** (`Sannel.Encoding.Runner`) in both install and Docker formats, then create a GitHub Release.

---

## Deployable Projects

| Project | SDK | Docker base image |
|---|---|---|
| `Sannel.Encoding.Manager.Web` | `Microsoft.NET.Sdk.Web` (Blazor/ASP.NET) | `mcr.microsoft.com/dotnet/aspnet:10.0` |
| `Sannel.Encoding.Runner` | `Microsoft.NET.Sdk.Worker` | `mcr.microsoft.com/dotnet/runtime:10.0` |

---

## Target Matrix

### Install bundles (self-contained, single-file)

| RID | Platform | Archive format |
|---|---|---|
| `win-x64` | Windows x64 | `.zip` |
| `win-arm64` | Windows ARM64 | `.zip` |
| `linux-x64` | Linux x64 | `.tar.gz` |
| `linux-arm64` | Linux ARM64 | `.tar.gz` |

Both the Server and the Runner are published for all four RIDs.

### Docker images

Docker targets Linux only (Windows containers are not in scope).

| Platform | Docker arch |
|---|---|
| Linux x64 | `linux/amd64` |
| Linux ARM64 | `linux/arm64` |

Images are pushed to **GitHub Container Registry (ghcr.io)** as multi-arch manifests:

- `ghcr.io/<owner>/encoding-manager-server:<tag>`
- `ghcr.io/<owner>/encoding-manager-runner:<tag>`

---

## Branching & Release Strategy

This project follows a GitFlow-inspired model with three promotion stages:

| Branch / Ref | Stage | GitHub Release type | Docker tags |
|---|---|---|---|
| `develop` | Preview | Pre-release | `develop`, `develop-<short-sha>` |
| `release/*` | Release Candidate (RC) | Pre-release | `rc`, `rc-<short-sha>` |
| `v*.*.*` tag | Official Release | Full release | `<version>`, `latest` |

**Promotion flow:**

```
develop  →  release/1.2.3  →  tag v1.2.3  →  merge to main
```

- Work happens on feature branches, merged into `develop`.
- When ready to cut a release, a `release/x.y.z` branch is created from `develop`.
- The tag `v*.*.*` is pushed from the release branch — this triggers the full release workflow.
- After the tag, the release workflow opens a PR (or direct merge) from the release branch into `main`.

---

## Workflow Overview

### `ci.yml` — Continuous Integration

**Triggers:** push to `main`, `develop`, `release/**`, and any pull request.

**Jobs:**
1. `build-and-test` — restore, build, and run tests (`dotnet test`).

---

### `preview.yml` — Preview Release (develop)

**Triggers:** push to `develop`.

**Jobs (run in parallel where possible):**

```
build-and-test
    ├── publish-server   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── publish-runner   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── docker-server    (multi-arch: linux/amd64, linux/arm64)
    └── docker-runner    (multi-arch: linux/amd64, linux/arm64)
            └── create-prerelease  (GitHub pre-release, tag: develop-<short-sha>)
```

- Docker images tagged `develop` and `develop-<short-sha>`.
- A single rolling GitHub pre-release at a fixed tag `develop` is created or updated on every push (overwrites previous assets).

---

### `rc.yml` — Release Candidate (release/*)

**Triggers:** push to `release/**`.

**Jobs (run in parallel where possible):**

```
build-and-test
    ├── publish-server   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── publish-runner   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── docker-server    (multi-arch: linux/amd64, linux/arm64)
    └── docker-runner    (multi-arch: linux/amd64, linux/arm64)
            └── create-prerelease  (GitHub pre-release, tag: rc-<short-sha>)
```

- Docker images tagged `rc` and `rc-<short-sha>`.
- A single rolling GitHub pre-release at a fixed tag `rc` is created or updated on every push (overwrites previous assets).

---

### `release.yml` — Official Release

**Triggers:** push of a tag matching `v*.*.*`.

**Jobs (run in parallel where possible):**

```
build-and-test
    ├── publish-server   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── publish-runner   (matrix: win-x64, win-arm64, linux-x64, linux-arm64)
    ├── docker-server    (multi-arch: linux/amd64, linux/arm64)
    └── docker-runner    (multi-arch: linux/amd64, linux/arm64)
            └── create-release   (needs all publish-* and docker-* jobs)
                    └── merge-to-main
```

#### `publish-server` / `publish-runner`

- Runs on `ubuntu-latest` (cross-compilation works for all RIDs).
- `dotnet publish --configuration Release --runtime <rid> --self-contained true -p:PublishSingleFile=true`
- Windows RIDs → zipped with `Compress-Archive` (pwsh); Linux RIDs → `tar -czf`.
- Uploads the archive as a workflow artifact.

#### `docker-server` / `docker-runner`

- Uses **Docker Buildx** with QEMU for cross-platform emulation.
- Multi-stage Dockerfiles (`sdk:10.0` build stage → slim runtime stage).
- Image tagged with the version from the git tag (e.g., `1.2.3`) and `latest`.
- Pushed to `ghcr.io`.

#### `create-release`

- Downloads all install artifacts from previous jobs.
- Creates a GitHub Release (with `--generate-notes` for auto changelog).
- Uploads all archives as release assets.
- Marked as a full (non-pre) release.

#### `merge-to-main`

- Runs after `create-release` succeeds.
- Opens a pull request from the release branch into `main` using the GitHub API (or `gh pr create`).
- This keeps `main` as the stable, always-releasable branch.

---

## Install / Uninstall Scripts

Each install bundle includes platform-appropriate install and uninstall scripts that are bundled **inside** the archive alongside the binary.

### Windows (`install.ps1` / `uninstall.ps1`)

- Must be run as Administrator.
- Prompts the user for an install directory; default: `C:\Program Files\Encoding Manager\<Server|Runner>`.
- **If the service already exists:** stops the service, copies updated files over the existing install, then restarts the service (update mode).
- **If the service does not exist:** copies files to the chosen directory, registers a Windows Service using `New-Service` (or `sc.exe`), and starts it (fresh install mode).
- `uninstall.ps1` stops the service, removes it, and deletes the install directory (with confirmation prompt).

### Linux (`install.sh` / `uninstall.sh`)

- Must be run as root (`sudo`).
- Prompts the user for an install directory; default: `/opt/sannel/encoding-manager/<server|runner>`.
- **If the systemd unit already exists:** stops the service, copies updated files over the existing install, then restarts the service (update mode).
- **If the unit does not exist:** copies files to the chosen directory, creates a `systemd` unit file at `/etc/systemd/system/sannel-encoding-<server|runner>.service`, runs `systemctl daemon-reload`, `systemctl enable`, and `systemctl start` (fresh install mode).
- `uninstall.sh` stops and disables the service, removes the unit file, reloads systemd, and deletes the install directory (with confirmation prompt).

> **Note:** The Windows ARM64 bundles use the same `install.ps1`/`uninstall.ps1` scripts as Windows x64. The Linux ARM64 bundles use the same `install.sh`/`uninstall.sh` as Linux x64.

---

## Files to Create / Modify

| File | Action | Purpose |
|---|---|---|
| `.github/workflows/ci.yml` | Create | Build + test on all branches and PRs |
| `.github/workflows/preview.yml` | Create | Preview pre-release on push to `develop` |
| `.github/workflows/rc.yml` | Create | RC pre-release on push to `release/**` |
| `.github/workflows/release.yml` | Create | Official release on `v*.*.*` tag + merge to main |
| `src/Sannel.Encoding.Manager.Web/Dockerfile` | Create | Multi-stage server image |
| `src/Sannel.Encoding.Runner/Dockerfile` | Create | Multi-stage runner image |
| `scripts/server/install.ps1` | Create | Windows install script for Server |
| `scripts/server/uninstall.ps1` | Create | Windows uninstall script for Server |
| `scripts/server/install.sh` | Create | Linux install script for Server |
| `scripts/server/uninstall.sh` | Create | Linux uninstall script for Server |
| `scripts/runner/install.ps1` | Create | Windows install script for Runner |
| `scripts/runner/uninstall.ps1` | Create | Windows uninstall script for Runner |
| `scripts/runner/install.sh` | Create | Linux install script for Runner |
| `scripts/runner/uninstall.sh` | Create | Linux uninstall script for Runner |
| `docs/docs/installation/server.md` | Create | Server installation documentation |
| `docs/docs/installation/runner.md` | Create | Runner installation documentation |
| `docs/docs/installation/docker-compose.md` | Create | Full-stack Docker Compose documentation |
| `docs/docs/installation/toc.yml` | Create | TOC for installation section |
| `docs/toc.yml` | Modify | Add Installation section link |

The `publish-server` and `publish-runner` workflow jobs copy the relevant scripts from `scripts/<component>/` into the publish output directory before archiving.

---

## Release Asset Naming

Each archive contains: the binary, `install.ps1`/`install.sh`, and `uninstall.ps1`/`uninstall.sh` for the respective platform.

```
server-win-x64.zip          (binary + install.ps1 + uninstall.ps1)
server-win-arm64.zip        (binary + install.ps1 + uninstall.ps1)
server-linux-x64.tar.gz     (binary + install.sh + uninstall.sh)
server-linux-arm64.tar.gz   (binary + install.sh + uninstall.sh)
runner-win-x64.zip          (binary + install.ps1 + uninstall.ps1)
runner-win-arm64.zip        (binary + install.ps1 + uninstall.ps1)
runner-linux-x64.tar.gz     (binary + install.sh + uninstall.sh)
runner-linux-arm64.tar.gz   (binary + install.sh + uninstall.sh)
```

---

## Documentation

Two new documentation pages are added under `docs/docs/installation/`, linked from the top-level `docs/toc.yml`.

### `docs/docs/installation/server.md` — Server Installation

Covers:
- Prerequisites (OS support matrix)
- **Windows install** — running `install.ps1`, default path (`C:\Program Files\Encoding Manager\Server`), service name, how to customize the install directory
- **Linux install** — running `install.sh`, default path (`/opt/sannel/encoding-manager/server`), systemd service name (`sannel-encoding-server`), how to customize the install directory
- **Uninstalling** — running the appropriate uninstall script on each platform
- **Docker** — `docker run` example using the `ghcr.io` image, environment variables, volume mounts, port mappings
- **Docker Compose** — full `docker-compose.yml` example for the server (with optional database sidecar), explanation of each service/volume/environment variable

### `docs/docs/installation/runner.md` — Runner Installation

Covers the same structure as the server doc but for the Runner:
- Windows default path: `C:\Program Files\Encoding Manager\Runner`
- Linux default path: `/opt/sannel/encoding-manager/runner`
- systemd service name: `sannel-encoding-runner`
- Windows service name: `sannel-encoding-runner`
- **Docker** — `docker run` example using the `ghcr.io` runner image, environment variables (server URL, runner token), volume mounts for media/scratch directories
- **Docker Compose** — full `docker-compose.yml` example for the runner alongside the server, showing how the two services connect

### `docs/docs/installation/docker-compose.md` — Full Stack Docker Compose

A dedicated page with a complete, production-ready `docker-compose.yml` example combining **both** the server and runner with a **PostgreSQL** database sidecar. Covers:
- Full service definitions (server + runner + postgres)
- All relevant environment variables with descriptions
- Volume definitions for persistent data and media
- Network configuration
- How to bring the stack up/down (`docker compose up -d`, `docker compose down`)

---

## Notes & Considerations

- **.NET 10**: Docker base images use `mcr.microsoft.com/dotnet/aspnet:10.0` and `mcr.microsoft.com/dotnet/runtime:10.0` — .NET 10 is fully released, no preview suffix needed.
- **GITHUB_TOKEN permissions**: The release workflow needs `contents: write` (for creating releases) and `packages: write` (for pushing to ghcr.io).
- **ARM64 cross-compilation**: `dotnet publish` supports cross-targeting via `--runtime` without needing a native ARM64 runner.
- **Docker Buildx + QEMU**: Required for building `linux/arm64` images on `ubuntu-latest` (x86). The `docker/setup-qemu-action` and `docker/setup-buildx-action` community actions handle this.
- **Solution filter**: All publish commands target individual `.csproj` files, not the solution, to avoid building migration/test projects unnecessarily.
