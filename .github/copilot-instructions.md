# Sannel Encoding Manager - Copilot Instructions

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

Migrations are split by provider into two separate folders, each with its own namespace, so that EF Core generates and maintains independent model snapshots per provider.

```
Features/Data/Migrations/
├── Sqlite/     # namespace: Sannel.Encoding.Manager.Web.Features.Data.Migrations.Sqlite
└── Postgres/   # namespace: Sannel.Encoding.Manager.Web.Features.Data.Migrations.Postgres
```

**Always supply both `--output-dir` and `--namespace` when adding a migration.** Omitting `--namespace` causes EF to overwrite the wrong provider's snapshot.

### Adding a new migration

```pwsh
# SQLite
dotnet ef migrations add <MigrationName> `
    --output-dir Features/Data/Migrations/Sqlite `
    --namespace "Sannel.Encoding.Manager.Web.Features.Data.Migrations.Sqlite"

# Postgres
$env:DB_PROVIDER = "postgres"
dotnet ef migrations add <MigrationName> `
    --output-dir Features/Data/Migrations/Postgres `
    --namespace "Sannel.Encoding.Manager.Web.Features.Data.Migrations.Postgres"
```

### How provider routing works at runtime

`ProviderAwareMigrationsAssembly` (registered via `options.ReplaceService<IMigrationsAssembly, ProviderAwareMigrationsAssembly>()`) filters the migrations and snapshot visible to EF at runtime by checking whether the migration class namespace contains `.Sqlite.` or `.Postgres.`, matching the active provider.
