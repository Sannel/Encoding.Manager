# Sannel Encoding Manager - Copilot Instructions

## Build, Test, and Run Commands

All commands run from the repository root.

```pwsh
# Restore, build, and test (full CI pass)
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --verbosity normal

# Run a single test class or method
dotnet test tests/Sannel.Encoding.Manager.HandBrake.Tests --filter "FullyQualifiedName~HandBrakeServiceTests"
dotnet test tests/Sannel.Encoding.Manager.HandBrake.Tests --filter "FullyQualifiedName~HandBrakeServiceTests.SomeMethodName"

# Run the web app locally
dotnet run --project src/Sannel.Encoding.Manager.Web/Sannel.Encoding.Manager.Web.csproj

# Run the encoding runner locally
dotnet run --project src/Sannel.Encoding.Runner/Sannel.Encoding.Runner.csproj
```

## Solution Structure

This is a multi-project .NET 10 solution (`Sannel.Encoding.Manager.slnx`):

| Project | Role |
|---|---|
| `Sannel.Encoding.Manager.Web` | Blazor Server web app — UI + API for managing encoding jobs |
| `Sannel.Encoding.Runner` | Worker service — polls the web API, runs HandBrakeCLI, ships logs back |
| `Sannel.Encoding.Manager.Data` | EF Core `AppDbContext`, all entity definitions, and the `Features/` data layer |
| `Sannel.Encoding.Manager.HandBrake` | HandBrakeCLI wrapper — scan, encode, parse JSON output |
| `Sannel.Encoding.Manager.Jellyfin` | Typed HTTP client for the Jellyfin API |
| `Sannel.Encoding.Manager.Migrations.Sqlite` | EF Core migrations for SQLite |
| `Sannel.Encoding.Manager.Migrations.Postgres` | EF Core migrations for PostgreSQL |

`AppDbContext` lives in `Sannel.Encoding.Manager.Data` but is referenced by the Web project via a project reference. Entity classes are co-located with their feature slices inside the Data project (`Features/<Feature>/Entities/`).

## Authentication

The web app uses **Microsoft Identity Web** (Azure AD / Entra ID) with two authentication schemes:

- **OpenIdConnect + Cookie** (`AzureAd` config section) — for interactive Blazor UI users.
- **`RunnerBearer`** JWT (`AzureAd` config section, `jwtBearerScheme: "RunnerBearer"`) — for the runner service calling `/api/` and `/hubs/` endpoints. Protected by the `"RunnerApi"` authorization policy.

All pages require authentication by default (fallback policy = `RequireAuthenticatedUser`). Use `[AllowAnonymous]` to opt out.

The runner (`Sannel.Encoding.Runner`) acquires tokens via `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:ClientSecret`, and `AzureAd:Scope` config keys.

## Encrypted Configuration

Both projects support an encrypted JSON config overlay that takes precedence over `appsettings.json`. Run the `configure` subcommand to set it up:

```pwsh
dotnet run --project src/Sannel.Encoding.Manager.Web -- configure
dotnet run --project src/Sannel.Encoding.Runner -- configure
```

Values prefixed with `enc:` in the config file are transparently decrypted at startup. This file should be in `.gitignore` (it may contain secrets).

## SignalR

`QueueHub` (`Features/Queue/Hubs/QueueHub.cs`) broadcasts real-time queue updates to connected Blazor UI clients. It is mapped at `/hubs/queue` and requires the `RunnerApi` policy for runner-originated pushes while UI clients use cookie auth.

## Testing

- **Framework**: xUnit + NSubstitute (mocking)
- Test projects live under `tests/`
- Use `NSubstitute.Substitute.For<T>()` to create mocks; see `HandBrakeServiceTests` for the established factory-method pattern (`CreateMockRunner`, `CreateService`).
- No test database setup needed — data-layer tests mock `AppDbContext` or use in-memory providers.

## Scripting Constraints

- **Do NOT use Python** for any scripting or command-line operations.
- Use **PowerShell** (`pwsh`) for all scripting tasks instead.

## File Editing

- When `replace_string_in_file` fails, **always** diagnose the reason (usually whitespace/indentation mismatch) and retry with corrected content — do NOT fall back to shell scripts to perform file edits.
- Re-read the exact surrounding lines from the file to get the correct literal whitespace, then retry the tool.

## Project Overview

This is a Blazor Server (.NET 10) web application for managing encoding workflows. The project uses **MudBlazor** as its UI component library and follows **Vertical Slice Architecture**.

## Architecture: Vertical Slice

This project is organized by **feature (vertical slice)**, not by technical concern (horizontal layers). Each feature contains all of its own UI components, pages, services, and models in one folder.

### Folder Structure

```
src/Sannel.Encoding.Manager.Web/
├── Components/                    # Root-level Blazor plumbing (App.razor, Routes.razor, _Imports.razor)
│   ├── App.razor                  # HTML host page
│   ├── Routes.razor               # Router configuration
│   ├── _Imports.razor             # Global usings for Components folder
│   └── Pages/                     # Infrastructure pages only (Error, NotFound)
│       ├── Error.razor
│       └── NotFound.razor
├── Features/                      # All feature slices live here
│   ├── _Imports.razor             # Shared usings for all features
│   ├── Shared/                    # Cross-cutting concerns shared across features
│   │   └── Layout/                # App shell layout
│   │       ├── MainLayout.razor   # MudBlazor layout with AppBar, Drawer, MainContent
│   │       ├── NavMenu.razor      # MudNavMenu navigation
│   │       ├── ReconnectModal.razor
│   │       ├── ReconnectModal.razor.css
│   │       └── ReconnectModal.razor.js
│   ├── Home/                      # Home feature
│   │   ├── Pages/
│   │   │   └── HomePage.razor
│   │   └── Components/            # (empty - no sub-components)
│   ├── Counter/                   # Counter feature
│   │   ├── Pages/
│   │   │   └── CounterPage.razor
│   │   └── Components/            # (empty - no sub-components)
│   ├── Filesystem/                # Filesystem feature
│   │   ├── Pages/
│   │   │   ├── FilesystemPage.razor
│   │   │   └── FilesystemPage.razor.cs
│   │   ├── Components/            # Reusable sub-components
│   │   ├── Dto/
│   │   │   ├── BrowseResponse.cs
│   │   │   ├── FileEntryResponse.cs
│   │   │   └── DirectoryEntryResponse.cs
│   │   ├── Controllers/
│   │   │   └── FilesystemController.cs
│   │   ├── Services/
│   │   │   ├── FilesystemService.cs
│   │   │   └── IFilesystemService.cs
│   │   ├── Repositories/          # Data access layer (if needed)
│   │   ├── Options/
│   │   │   └── FilesystemOptions.cs
│   │   └── _Imports.razor         # Feature-specific usings (optional)
│   └── Weather/                   # Weather feature
│       ├── Pages/
│       │   └── WeatherPage.razor
│       └── Components/            # (empty - no sub-components)
├── Program.cs                     # App startup and DI configuration
└── wwwroot/                       # Static assets
    └── app.css                    # Minimal app-level CSS overrides
```

### Adding a New Feature

When creating a new feature:

1. Create a new folder under `Features/` named after the feature (e.g., `Features/Encoding/`).
2. Organize all related files into the following subfolders:
   - **Pages**: Page components with `@page` directives that serve as views for this feature
   - **Components**: Reusable sub-components (child components, partials) for this feature
   - **Dto**: Data transfer objects and models specific to this feature
   - **Controllers**: ASP.NET Core controllers for API endpoints
   - **Services**: Business logic services for this feature
   - **Repositories**: Data access/repository classes for this feature (if needed)
   - **Options**: Configuration and options classes for this feature
3. Register any new services in `Program.cs`.
4. Add a navigation entry in `Features/Shared/Layout/NavMenu.razor`.

**Do NOT** place new pages in `Components/Pages/` — that folder is reserved for infrastructure pages (Error, NotFound).

### Example Feature Structure

```
Features/Encoding/
├── Pages/                      # Page components with @page directive
│   ├── EncodingPage.razor      # @page "/encoding"
│   └── EncodingPage.razor.cs   # Code-behind with partial class
├── Components/                 # Reusable sub-components for this feature
│   ├── EncodingForm.razor      # Child component for the form
│   └── EncodingList.razor      # Child component for listing items
├── Dto/                        # DTOs and models for this feature
│   ├── EncodingJob.cs          # DTO for encoding jobs
│   ├── EncodingRequest.cs      # Request DTO
│   └── EncodingResponse.cs     # Response DTO
├── Controllers/                # API controllers for this feature
│   └── EncodingController.cs   # REST API endpoints
├── Services/                   # Business logic services
│   ├── EncodingService.cs      # Service for encoding operations
│   └── IEncodingService.cs     # Service interface
├── Repositories/               # Data access layer (if needed)
│   ├── EncodingRepository.cs   # Repository for data access
│   └── IEncodingRepository.cs  # Repository interface
├── Options/                    # Configuration and options
│   └── EncodingOptions.cs      # Configuration options
└── _Imports.razor              # Optional: feature-specific usings (advanced)
```

### Blazor Code-Behind Pattern

For Blazor pages (`.razor` files), use code-behind files (`.razor.cs`) instead of `@code` blocks:

**MyPage.razor:**
```razor
@page "/mypage"
@rendermode InteractiveServer

<PageTitle>My Page</PageTitle>

<MudText>@_message</MudText>
<MudButton OnClick="HandleClick">Click Me</MudButton>
```

**MyPage.razor.cs:**
```csharp
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Sannel.Encoding.Manager.Web.Features.MyFeature;

public partial class MyPage : ComponentBase
{
	[Inject]
	private IMyService MyService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private string _message = "Hello";

	protected override async Task OnInitializedAsync()
	{
		this._message = await this.MyService.GetMessageAsync();
	}

	private void HandleClick()
	{
		this.Snackbar.Add("Clicked!", Severity.Success);
	}
}
```

**Key points:**
- Code-behind class is `partial` and inherits from `ComponentBase`
- Dependencies injected via `[Inject]` attribute on properties
- Use `this.` qualifier for all member access (fields, properties, methods)
- No `@inject` directives in the `.razor` file when using code-behind

## UI Framework: MudBlazor

- **MudBlazor** (v9.x) is the **sole UI component library**. Do not use Bootstrap or raw HTML for controls.
- All MudBlazor providers are configured in `Features/Shared/Layout/MainLayout.razor`:
  - `MudThemeProvider` (with light/dark mode toggle)
  - `MudPopoverProvider`
  - `MudDialogProvider`
  - `MudSnackbarProvider`
- MudBlazor services are registered in `Program.cs` via `builder.Services.AddMudServices()`.
- MudBlazor CSS and JS are loaded in `Components/App.razor`.
- The `@using MudBlazor` directive is in both `Components/_Imports.razor` and `Features/_Imports.razor`.

### MudBlazor Usage Guidelines

- Use `MudText` instead of `<h1>`, `<p>`, etc. with appropriate `Typo` values.
- Use `MudButton` instead of `<button>`.
- Use `MudTable` instead of `<table>`.
- Use `MudTextField`, `MudSelect`, `MudCheckBox`, etc. for form inputs.
- Use `MudDialog` for modals/dialogs.
- Use `MudSnackbar` (injected `ISnackbar`) for toast notifications.
- Use `MudCard` for content containers.
- Use `MudGrid` / `MudItem` for responsive layouts.
- Use MudBlazor `Color` enum values for theming consistency.
- Refer to [MudBlazor documentation](https://mudblazor.com/docs/overview) for component reference.

## Rendering Modes

- The app uses **Blazor Server** with interactive server rendering.
- Pages that need interactivity should include `@rendermode InteractiveServer`.
- Pages using streaming should include `@attribute [StreamRendering]`.

## Key Conventions

- **Namespace convention**: `Sannel.Encoding.Manager.Web.Features.<FeatureName>`
- **Page naming**: Feature pages are named `<Feature>Page.razor` (e.g., `HomePage.razor`, `CounterPage.razor`).
- **Code-behind files**: Blazor pages should use code-behind files (`.razor.cs`) instead of `@code` blocks. The code-behind class should be a `partial class` inheriting from `ComponentBase`, with injected dependencies as properties marked with `[Inject]` attribute.
- **Private fields**: Use underscore prefix (e.g., `_currentCount`, `_forecasts`).
- **Nullable**: Nullable reference types are enabled project-wide.
- **Implicit usings**: Enabled — no need to import common .NET namespaces.
- **.NET version**: net10.0

## Code Style (.editorconfig)

These rules are enforced via `.editorconfig` and must be followed in all generated C# code.

### File Organization
- **One public class/struct/record per file** — each file should contain only a single public type.
- Private types and utilities within a file are acceptable.
- Use clear, descriptive filenames matching the public type name (e.g., `MyClass.cs` for `public class MyClass`).

### Formatting
- **Indentation**: Tabs (not spaces) for all files except `.yml` (2 spaces).
- **Line endings**: CRLF.
- **Braces**: Always on their own line (`csharp_new_line_before_open_brace = all`). `else`, `catch`, and `finally` also go on their own line.
- **Braces are required** for all control flow blocks — never omit them (`csharp_prefer_braces = true:error`).
- Single-line statements are not allowed (`csharp_preserve_single_line_statements = false`).
- Single-line blocks are preserved (`csharp_preserve_single_line_blocks = true`).

### `var` usage
- Use `var` for built-in types (`csharp_style_var_for_built_in_types = true:warning`).
- Use `var` when the type is apparent from the right-hand side (`csharp_style_var_when_type_is_apparent = true:suggestion`).
- Use `var` elsewhere where appropriate (`csharp_style_var_elsewhere = true:suggestion`).

### Language features
- Prefer expression-bodied members for methods, constructors, operators, properties, indexers, and accessors.
- Prefer pattern matching over `is`-with-cast and `as`-with-null-check.
- Prefer object and collection initializers.
- Prefer null-coalescing (`??`) and null-conditional (`?.`) operators.
- Prefer `throw` expressions and conditional delegate calls.
- Use explicit tuple names (`dotnet_style_explicit_tuple_names = true:warning`).
- Qualify field, property, method, and event access with `this.` (`dotnet_style_qualification_for_* = true:silent`).
- Use language keywords (`int`, `string`, etc.) instead of BCL type names for both locals/parameters and member access.

## Database Migrations

This project supports **two database providers**: SQLite and PostgreSQL. Each provider has its own dedicated migration project with an `IDesignTimeDbContextFactory` implementation:

```
src/
├── Sannel.Encoding.Manager.Migrations.Sqlite/
│   ├── Sannel.Encoding.Manager.Migrations.Sqlite.csproj
│   ├── SqliteDesignTimeDbContextFactory.cs
│   └── Migrations/                # namespace: Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
│       ├── <timestamp>_<Name>.cs
│       ├── <timestamp>_<Name>.Designer.cs
│       └── AppDbContextModelSnapshot.cs
└── Sannel.Encoding.Manager.Migrations.Postgres/
    ├── Sannel.Encoding.Manager.Migrations.Postgres.csproj
    ├── PostgresDesignTimeDbContextFactory.cs
    └── Migrations/                # namespace: Sannel.Encoding.Manager.Migrations.Postgres.Migrations
        ├── <timestamp>_<Name>.cs
        ├── <timestamp>_<Name>.Designer.cs
        └── AppDbContextModelSnapshot.cs
```

### CRITICAL: Every schema change requires migrations for BOTH providers

**When any entity model changes, you MUST create a migration in BOTH the SQLite and Postgres projects.** Forgetting one provider will cause runtime failures when that provider is selected. Always run both commands below.

### Adding a new migration

**Always supply `--project`, `--output-dir`, and `--namespace`.** Omitting `--namespace` causes EF to place the migration in the wrong namespace and overwrite the wrong provider's snapshot.

Run both commands from the repository root:

```pwsh
# 1. SQLite — always create this migration
dotnet ef migrations add <MigrationName> `
    --project src/Sannel.Encoding.Manager.Migrations.Sqlite/Sannel.Encoding.Manager.Migrations.Sqlite.csproj `
    --output-dir Migrations `
    --namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations

# 2. Postgres — always create this migration too
dotnet ef migrations add <MigrationName> `
    --project src/Sannel.Encoding.Manager.Migrations.Postgres/Sannel.Encoding.Manager.Migrations.Postgres.csproj `
    --output-dir Migrations `
    --namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
```

### ### Version Management

The project uses `CHANGELOG.md` to track versions and changes.
- The current version is **0.0.1**.
- Always update `CHANGELOG.md` when making changes to reflect the work performed in the current version.
