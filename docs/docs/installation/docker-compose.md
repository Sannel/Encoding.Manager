# Docker Compose

This guide provides a complete `docker-compose.yml` for running the **Encoding Manager Server**, **Runner**, and **PostgreSQL** database together.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/install/) (v2+)
- A Microsoft Entra (Azure AD) app registration (see [Getting Started](../getting-started.md))

---

## Full Stack Example

Create a `docker-compose.yml` file:

```yaml
services:
  db:
    image: postgres:17
    restart: unless-stopped
    environment:
      POSTGRES_USER: encoding_manager
      POSTGRES_PASSWORD: ${DB_PASSWORD:?Set DB_PASSWORD in .env}
      POSTGRES_DB: encoding_manager
    volumes:
      - db-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U encoding_manager"]
      interval: 10s
      timeout: 5s
      retries: 5

  server:
    image: ghcr.io/sannel/encoding-manager-server:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      DB_PROVIDER: postgres
      ConnectionStrings__DefaultConnection: >-
        Host=db;Port=5432;Database=encoding_manager;
        Username=encoding_manager;Password=${DB_PASSWORD}
      AzureAd__Instance: ${AZURE_AD_INSTANCE:-https://login.microsoftonline.com/}
      AzureAd__TenantId: ${AZURE_AD_TENANT_ID:?Set AZURE_AD_TENANT_ID in .env}
      AzureAd__ClientId: ${AZURE_AD_CLIENT_ID:?Set AZURE_AD_CLIENT_ID in .env}
      AzureAd__ClientSecret: ${AZURE_AD_CLIENT_SECRET:?Set AZURE_AD_CLIENT_SECRET in .env}
    volumes:
      - server-data:/app/data
    depends_on:
      db:
        condition: service_healthy

  runner:
    image: ghcr.io/sannel/encoding-manager-runner:latest
    restart: unless-stopped
    environment:
      Runner__ServerUrl: http://server:8080
      Runner__Name: runner-01
      HandBrake__CliPath: /usr/bin/HandBrakeCLI
      Filesystem__ScratchPath: /scratch
      AzureAd__Instance: ${AZURE_AD_INSTANCE:-https://login.microsoftonline.com/}
      AzureAd__TenantId: ${AZURE_AD_TENANT_ID}
      AzureAd__ClientId: ${AZURE_AD_CLIENT_ID}
      AzureAd__ClientSecret: ${AZURE_AD_CLIENT_SECRET}
    volumes:
      - /path/to/media:/media
      - runner-scratch:/scratch
    depends_on:
      - server

volumes:
  db-data:
  server-data:
  runner-scratch:
```

---

## Environment File

Create a `.env` file next to your `docker-compose.yml`:

```env
# Database
DB_PASSWORD=choose-a-strong-password

# Microsoft Entra (Azure AD)
AZURE_AD_INSTANCE=https://login.microsoftonline.com/
AZURE_AD_TENANT_ID=your-tenant-id
AZURE_AD_CLIENT_ID=your-client-id
AZURE_AD_CLIENT_SECRET=your-client-secret
```

> **Security:** Never commit your `.env` file to source control. Add it to `.gitignore`.

---

## Service Reference

### `db` — PostgreSQL

| Setting | Value |
|---|---|
| Image | `postgres:17` |
| Port | `5432` (internal only) |
| Volume | `db-data` — persistent database storage |
| Health check | `pg_isready` polls until the database is accepting connections |

### `server` — Encoding Manager Server

| Setting | Value |
|---|---|
| Image | `ghcr.io/sannel/encoding-manager-server:latest` |
| Port | `8080` — web UI and API |
| Volume | `server-data` — application data |
| Depends on | `db` (healthy) |

### `runner` — Encoding Manager Runner

| Setting | Value |
|---|---|
| Image | `ghcr.io/sannel/encoding-manager-runner:latest` |
| Volumes | `/path/to/media:/media` — shared media library |
| | `runner-scratch` — temporary encoding workspace |
| Depends on | `server` |

---

## Usage

### Start all services

```bash
docker compose up -d
```

### View logs

```bash
# All services
docker compose logs -f

# Single service
docker compose logs -f server
```

### Stop all services

```bash
docker compose down
```

### Stop and remove volumes (⚠ deletes data)

```bash
docker compose down -v
```

---

## Scaling Runners

To run multiple runners, scale the runner service:

```bash
docker compose up -d --scale runner=3
```

Each runner instance will independently poll the server for encoding jobs. Give each runner a unique name by overriding the environment variable:

```yaml
  runner-02:
    image: ghcr.io/sannel/encoding-manager-runner:latest
    restart: unless-stopped
    environment:
      Runner__ServerUrl: http://server:8080
      Runner__Name: runner-02
      HandBrake__CliPath: /usr/bin/HandBrakeCLI
      Filesystem__ScratchPath: /scratch
      AzureAd__Instance: ${AZURE_AD_INSTANCE:-https://login.microsoftonline.com/}
      AzureAd__TenantId: ${AZURE_AD_TENANT_ID}
      AzureAd__ClientId: ${AZURE_AD_CLIENT_ID}
      AzureAd__ClientSecret: ${AZURE_AD_CLIENT_SECRET}
    volumes:
      - /path/to/media:/media
      - runner-02-scratch:/scratch
    depends_on:
      - server
```

---

## Updating

To update to a new version:

```bash
docker compose pull
docker compose up -d
```

This pulls the latest images and recreates containers with zero configuration changes.
