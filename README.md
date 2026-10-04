# Encoding Manager Project

This repository contains **Sannel.Encoding.Manager**, a robust, multi-project platform built on .NET 10 for managing complex media encoding workflows (e.g., video transcoding via HandBrakeCLI).

The system acts as an integrated pipeline to queue, monitor, and execute resource-intensive jobs, providing a unified management UI and dedicated worker services.

## ✨ Key Features

*   **Workflow Management:** End-to-end job lifecycle from submission to completion.
*   **Real-Time Monitoring:** Uses SignalR for live status updates on encoding progress.
*   **Multi-Platform Support:** Supports different database backends (SQLite, PostgreSQL).
*   **Integrated Media Handling:** Includes wrappers for industry tools like HandBrakeCLI and API clients for services like Jellyfin.

## 🏗️ Architecture & Project Structure

The solution follows a Vertical Slice Architecture, keeping features logically separated. Key components include:

*   **`src/Sannel.Encoding.Manager.Web`** (Blazor Server): The Blazor UI and primary API endpoint. It handles user interactions, authentication, and queuing of new jobs.
*   **`src/Sannel.Encoding.Manager.Runner`** (Worker Service): A background service that consumes job requests from the API, executes the physical encoding tasks, and reports status back to the web front-end.
*   **`src/Sannel.Encoding.Manager.Data`**: The core data access layer defining all entities (`AppDbContext`).
*   **`src/Sannel.Encoding.Manager.HandBrake`**: Dedicated logic for wrapping HandBrakeCLI and parsing its output.
*   **`src/Sannel.Encoding.Manager.Jellyfin`**: Client library for integrating with the Jellyfin media API.
*   **`tests/`**: Contains the xUnit test suite for unit testing business logic.

## 🚀 Getting Started

### Prerequisites

*   [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
*   HandBrakeCLI (Must be accessible in system PATH)
*   Database provider (e.g., `sqlite3` or `psql`)

### Running the Application

The application is composed of several services and projects:

1.  **Web UI:** To run the front-end:

    ```powershell
    dotnet run --project src/Sannel.Encoding.Manager.Web
    ```

2.  **Runner Service:** To start the job executor:

    ```powershell
    dotnet run --project src/Sannel.Encoding.Manager.Runner
    ```

3.  **Configuration:** Sensitive configurations (like database connection strings) should be handled via encrypted config overlays for production use.

## ⚙️ Database Setup & Migrations

All schema changes require running migrations for **both** supported providers (SQLite and PostgreSQL).

### Adding a New Migration

```powershell
# 1. SQLite Migration
dotnet ef migrations add <MigrationName> `
    --project src/Sannel.Encoding.Manager.Migrations.Sqlite/Sannel.Encoding.Manager.Migrations.Sqlite.csproj `
    --output-dir Migrations `
    --namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations

# 2. PostgreSQL Migration
dotnet ef migrations add <MigrationName> `
    --project src/Sannel.Encoding.Manager.Migrations.Postgres/Sannel.Encoding.Manager.Migrations.Postgres.csproj `
    --output-dir Migrations `
    --namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
```

## 🤝 Contributing

*   Please read the project's detailed architectural guidelines for creating new features.
*   Use standard .NET coding conventions and follow the [Project Style Guide](link-to-style-guide).
*   Remember to update migration scripts for *all* involved database providers whenever schema changes occur.
