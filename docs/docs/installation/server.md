# Server Installation

This guide covers installing the **Encoding Manager Server** — the Blazor web UI and API that manages encoding workflows.

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

### Install / Update

1. Download `server-win-x64.zip` (or `server-win-arm64.zip` for ARM devices) from the [Releases](https://github.com/Sannel/Encoding.Manager/releases) page.
2. Extract the archive.
3. Open PowerShell **as Administrator** and run:

```powershell
.\install.ps1
```

You will be prompted for an install directory. Press **Enter** to accept the default:

```
C:\Program Files\Encoding Manager\Server
```

The script will:
- Copy files to the install directory
- Register and start a Windows Service named `sannel-encoding-server`

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

### Install / Update

1. Download `server-linux-x64.tar.gz` (or `server-linux-arm64.tar.gz` for ARM devices) from the [Releases](https://github.com/Sannel/Encoding.Manager/releases) page.
2. Extract the archive:

```bash
mkdir -p server && tar -xzf server-linux-x64.tar.gz -C server
```

3. Run the install script:

```bash
sudo ./server/install.sh
```

You will be prompted for an install directory. Press **Enter** to accept the default:

```
/opt/sannel/encoding-manager/server
```

The script will:
- Copy files to the install directory
- Create a systemd unit at `/etc/systemd/system/sannel-encoding-server.service`
- Enable and start the service

If the service is **already installed**, the script automatically stops it, copies the updated files, and restarts it.

### Service Management

```bash
sudo systemctl status sannel-encoding-server
sudo systemctl stop sannel-encoding-server
sudo systemctl start sannel-encoding-server
sudo systemctl restart sannel-encoding-server
sudo journalctl -u sannel-encoding-server -f
```

### Uninstall

```bash
sudo ./uninstall.sh
```

This stops and disables the service, removes the systemd unit file, and (after confirmation) deletes the install directory.

---

## Docker

The server is available as a multi-architecture Docker image supporting `linux/amd64` and `linux/arm64`.

### Image

```
ghcr.io/sannel/encoding-manager-server:<tag>
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
  --name encoding-manager-server \
  -p 8080:8080 \
  -v encoding-manager-data:/app/data \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/encoding.db" \
  -e DB_PROVIDER="sqlite" \
  ghcr.io/sannel/encoding-manager-server:latest
```

### Environment Variables

| Variable | Description | Example |
|---|---|---|
| `DB_PROVIDER` | Database provider (`sqlite` or `postgres`) | `sqlite` |
| `ConnectionStrings__DefaultConnection` | Database connection string | See below |
| `AzureAd__Instance` | Microsoft Entra instance URL | `https://login.microsoftonline.com/` |
| `AzureAd__TenantId` | Entra tenant ID | `your-tenant-id` |
| `AzureAd__ClientId` | Entra client (application) ID | `your-client-id` |
| `AzureAd__ClientSecret` | Entra client secret | `your-client-secret` |
| `ASPNETCORE_URLS` | Listening URLs | `http://+:8080` |

#### Connection String Examples

**SQLite:**
```
Data Source=/app/data/encoding.db
```

**PostgreSQL:**
```
Host=db;Port=5432;Database=encoding_manager;Username=postgres;Password=yourpassword
```

### Volumes

| Mount Point | Purpose |
|---|---|
| `/app/data` | SQLite database and application data |

### Docker Compose

See the [Docker Compose](docker-compose.md) guide for a full-stack example with the server, runner, and PostgreSQL.
