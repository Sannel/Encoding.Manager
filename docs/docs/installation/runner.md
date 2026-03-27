# Runner Installation

This guide covers installing the **Encoding Manager Runner** — the background worker service that claims encoding jobs from the server and processes them using HandBrake.

## Supported Platforms

| Platform | Architecture | Method |
|---|---|---|
| Windows | x64, ARM64 | Install script or Docker |
| Linux | x64, ARM64 | Install script or Docker |

---

## Install Script (Windows)

### Prerequisites

- Windows 10/11 or Windows Server 2019+
- PowerShell 5.1+ (built-in) or PowerShell 7+
- Administrator privileges
- [HandBrakeCLI](https://handbrake.fr/downloads2.php) installed and accessible in PATH (or configured via `HandBrake:CliPath`)

### Install / Update

1. Download `runner-win-x64.zip` (or `runner-win-arm64.zip` for ARM devices) from the [Releases](https://github.com/Sannel/Encoding.Manager/releases) page.
2. Extract the archive.
3. Open PowerShell **as Administrator** and run:

```powershell
.\install.ps1
```

You will be prompted for an install directory. Press **Enter** to accept the default:

```
C:\Program Files\Encoding Manager\Runner
```

The script will:
- Copy files to the install directory
- Register and start a Windows Service named `sannel-encoding-runner`

If the service is **already installed**, the script automatically stops it, copies the updated files, and restarts it.

### Uninstall

From the install directory, run as Administrator:

```powershell
.\uninstall.ps1
```

This stops the service, removes it, and (after confirmation) deletes the install directory.

---

## Install Script (Linux)

### Prerequisites

- A systemd-based Linux distribution (Ubuntu 20.04+, Debian 11+, RHEL 8+, etc.)
- Root access (`sudo`)
- [HandBrakeCLI](https://handbrake.fr/docs/en/latest/get-handbrake/download-and-install.html) installed (e.g., `sudo apt install handbrake-cli`)

### Install / Update

1. Download `runner-linux-x64.tar.gz` (or `runner-linux-arm64.tar.gz` for ARM devices) from the [Releases](https://github.com/Sannel/Encoding.Manager/releases) page.
2. Extract the archive:

```bash
mkdir -p runner && tar -xzf runner-linux-x64.tar.gz -C runner
```

3. Run the install script:

```bash
sudo ./runner/install.sh
```

You will be prompted for an install directory. Press **Enter** to accept the default:

```
/opt/sannel/encoding-manager/runner
```

The script will:
- Copy files to the install directory
- Create a systemd unit at `/etc/systemd/system/sannel-encoding-runner.service`
- Enable and start the service

If the service is **already installed**, the script automatically stops it, copies the updated files, and restarts it.

### Service Management

```bash
sudo systemctl status sannel-encoding-runner
sudo systemctl stop sannel-encoding-runner
sudo systemctl start sannel-encoding-runner
sudo systemctl restart sannel-encoding-runner
sudo journalctl -u sannel-encoding-runner -f
```

### Uninstall

```bash
sudo ./uninstall.sh
```

This stops and disables the service, removes the systemd unit file, and (after confirmation) deletes the install directory.

---

## Docker

The runner is available as a multi-architecture Docker image supporting `linux/amd64` and `linux/arm64`.

### Image

```
ghcr.io/sannel/encoding-manager-runner:<tag>
```

| Tag | Description |
|---|---|
| `latest` | Latest stable release |
| `x.y.z` | Specific version (e.g., `1.0.0`) |
| `rc` | Latest release candidate |
| `develop` | Latest preview build |

### Quick Start

```bash
docker run -d \
  --name encoding-manager-runner \
  -v /path/to/media:/media \
  -v encoding-runner-scratch:/scratch \
  -e Runner__ServerUrl="http://encoding-manager-server:8080" \
  -e Runner__Name="runner-01" \
  ghcr.io/sannel/encoding-manager-runner:latest
```

### Environment Variables

| Variable | Description | Example |
|---|---|---|
| `Runner__ServerUrl` | URL of the Encoding Manager Server | `http://server:8080` |
| `Runner__Name` | Display name for this runner | `runner-01` |
| `Runner__PollIntervalSeconds` | How often to check for new jobs | `30` |
| `HandBrake__CliPath` | Path to HandBrakeCLI binary | `/usr/bin/HandBrakeCLI` |
| `Filesystem__ScratchPath` | Temporary working directory | `/scratch` |
| `AzureAd__Instance` | Microsoft Entra instance URL | `https://login.microsoftonline.com/` |
| `AzureAd__TenantId` | Entra tenant ID | `your-tenant-id` |
| `AzureAd__ClientId` | Entra client (application) ID | `your-client-id` |
| `AzureAd__ClientSecret` | Entra client secret | `your-client-secret` |

### Volumes

| Mount Point | Purpose |
|---|---|
| `/media` | Shared media library (source files and encoded output) |
| `/scratch` | Temporary working directory for active encoding jobs |

### Docker Compose

See the [Docker Compose](docker-compose.md) guide for a full-stack example with the server, runner, and PostgreSQL.
