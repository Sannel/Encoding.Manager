# Sannel Encoding Manager - Copilot Instructions

## Scripting Constraints

- **Do NOT use Python** for any scripting or command-line operations.
- Use **PowerShell** (`pwsh`) for all scripting tasks instead.

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
│   │   └── HomePage.razor
│   ├── Counter/                   # Counter feature
│   │   └── CounterPage.razor
│   └── Weather/                   # Weather feature
│       └── WeatherPage.razor
├── Program.cs                     # App startup and DI configuration
└── wwwroot/                       # Static assets
    └── app.css                    # Minimal app-level CSS overrides
```

### Adding a New Feature

When creating a new feature:

1. Create a new folder under `Features/` named after the feature (e.g., `Features/Encoding/`).
2. Place all related files inside that folder:
   - **Page component**: `EncodingPage.razor` (with `@page` directive)
   - **Sub-components**: Any child components specific to this feature (e.g., `EncodingForm.razor`, `EncodingList.razor`)
   - **Services**: Feature-specific services (e.g., `EncodingService.cs`)
   - **Models**: Feature-specific DTOs/models (e.g., `EncodingJob.cs`)
3. Register any new services in `Program.cs`.
4. Add a navigation entry in `Features/Shared/Layout/NavMenu.razor`.

**Do NOT** place new pages in `Components/Pages/` — that folder is reserved for infrastructure pages (Error, NotFound).

### Example Feature Structure

```
Features/Encoding/
├── EncodingPage.razor          # @page "/encoding"
├── EncodingForm.razor          # Child component for the form
├── EncodingList.razor          # Child component for listing items
├── EncodingService.cs          # Service for encoding business logic
└── EncodingJob.cs              # Model/DTO for encoding jobs
```

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
- **Private fields**: Use underscore prefix (e.g., `_currentCount`, `_forecasts`).
- **Nullable**: Nullable reference types are enabled project-wide.
- **Implicit usings**: Enabled — no need to import common .NET namespaces.
- **.NET version**: net10.0

## Code Style (.editorconfig)

These rules are enforced via `.editorconfig` and must be followed in all generated C# code.

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
