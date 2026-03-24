# Scanning Feature Plan

## Overview

Add a **Scan** feature that lets a user scan a selected disc folder using HandBrakeCLI and then browse the discovered titles or chapters. The entry point is the existing "Select Disc" button in the Filesystem browser — clicking it will navigate to the new Scan page instead of merely displaying the selection inline.

---

## 1. Navigation Change: Filesystem → Scan

### Current Behaviour
`SelectDiscFolder()` in `FilesystemPage.razor.cs` sets a local `_selectedItem` string and shows a MudBlazor snackbar. Nothing further happens.

### New Behaviour
Inject `NavigationManager` into `FilesystemPage` and call `NavigateTo` when a disc is selected, passing the root label, relative path, and disc type as query-string parameters:

```
/scan?root=<url-encoded-root-label>&path=<url-encoded-relative-path>&discType=<BluRay|Dvd|...>
```

All paths remain relative to a configured root — physical paths are **never** exposed in the URL. The `root` value is the same label already used by `BrowseAsync`. The `path` value is the relative path built during browsing (e.g. `Movies/Die Hard`). The disc type comes from `DirectoryEntryResponse.DiscType`.

`ScanPage` will pass `root` + `path` to `IFilesystemService` (or a new overload/helper) to resolve the physical path server-side before calling `IHandBrakeService.ScanAsync()`.

---

## 2. Feature Folder Structure

```
Features/Scan/
├── Pages/
│   ├── ScanPage.razor          # @page "/scan"  — top-level scan page
│   └── ScanPage.razor.cs       # code-behind
├── Components/
│   ├── TitlesModeView.razor    # Titles mode UI (receives scan result as parameter)
│   └── ChaptersModeView.razor  # Chapters mode UI (receives scan result as parameter)
└── Dto/
    └── ScanMode.cs             # enum: Titles, Chapters
```

No new service is needed — all scan work goes through the existing `IHandBrakeService.ScanAsync()`.

A navigation entry `Scan` pointing to `/scan` is **not** added to `NavMenu.razor` because this page is always reached by selecting a disc; it is not a free-standing destination.

---

## 3. ScanPage — Landing Page

### Query Parameters
| Parameter | Type | Required | Notes |
|---|---|---|---|
| `root` | `string` | Yes | URL-encoded root label (matches a configured directory) |
| `path` | `string` | Yes | URL-encoded relative path within that root (e.g. `Movies/Die Hard`) |
| `discType` | `string` | No | Display only (e.g. `BluRay`, `Dvd`) |

Physical path resolution happens entirely server-side — `IFilesystemService` resolves `root` + `path` to an absolute path before it is passed to `IHandBrakeService.ScanAsync()`. Absolute paths are never surfaced in URLs or the browser.

### Path Traversal Protection

`IFilesystemService.ResolvePhysicalPathAsync` (or its equivalent) **must** reject any `path` value that could escape the configured root. This validation applies before any file-system access occurs:

1. **Segment check** — split the relative path on `/` and `\`; if any segment is `..` or `.` (after trimming whitespace) reject it immediately.
2. **Canonical resolution check** — after joining the root's base path with the relative path, call `Path.GetFullPath()` on the result and verify the canonical absolute path **starts with** the root's canonical base path (also normalised via `Path.GetFullPath`). This catches encoded or multi-step traversal attempts (e.g. `a/../../etc`) that survive the segment check.
3. **Null / empty segments** — reject paths with empty segments (e.g. `//` or leading `/`).

If any check fails, `ResolvePhysicalPathAsync` throws an `ArgumentException` (or returns a typed error). `ScanPage` catches this and displays a `MudAlert` with the message **"Invalid path: the specified path is not allowed."** and a "Go back to Filesystem" link. No further processing occurs.

### Page Load Sequence
1. Parse `root` and `path` from the query string. If either is missing or empty show an error and offer a "Go back to Filesystem" link.
2. Resolve the physical path server-side; if path traversal validation fails show an error and stop (see Path Traversal Protection above).
3. Display the root label and relative path as read-only context at the top of the page (never the physical path).
4. Immediately trigger `IHandBrakeService.ScanAsync(resolvedPhysicalPath)` (show `MudProgressCircular` while in progress).
5. On success, show the mode controls and results area.
6. On failure, show a `MudAlert` with the error message and a **Retry** button that re-runs `ScanAsync` without leaving the page.

### Mode Controls (shown after a successful scan)
- A `MudSelect<ScanMode>` labelled **"Mode"** with two items:
  - `Titles` (default)
  - `Chapters`
- Changing the mode swaps the child component rendered below.

---

## 4. Titles Mode (`TitlesModeView`)

### Parameters
- `ScanResult` — the `HandBrakeScanResult` passed from the parent page.

### Controls
- A `MudNumericField<int>` labelled **"Minimum Duration (seconds)"**, defaulting to `30`, minimum value `0`.

### Display
Render a `MudTable` filtered to titles where `Duration.TotalSeconds >= minimumSeconds` (inclusive — a title of exactly the minimum duration is included). The table updates reactively as the user changes the minimum-duration field (no extra button press needed). Rows are **not** clickable; there is no drill-down into individual titles from this mode.

| Column | Source |
|---|---|
| Title # | `TitleInfo.TitleNumber` |
| Duration | `TitleInfo.Duration` formatted as `h:mm:ss` |
| Resolution | `TitleInfo.Width × TitleInfo.Height` |
| Frame Rate | `TitleInfo.FrameRate` |
| Audio Tracks | `TitleInfo.AudioTracks.Count` |
| Subtitles | `TitleInfo.Subtitles.Count` |
| Chapters | `TitleInfo.Chapters.Count` |

If no titles pass the filter, show a `MudAlert` info message: "No titles found matching the minimum duration."

---

## 5. Chapters Mode (`ChaptersModeView`)

### Parameters
- `ScanResult` — the `HandBrakeScanResult` passed from the parent page.

### Controls
- A `MudSelect<TitleInfo?>` labelled **"Title"**, listing every title in the scan result with no duration filter.
  - Each item displays: `Title {TitleInfo.TitleNumber}  —  {Duration h:mm:ss}  ({Chapters.Count} chapters)`
  - Default selection: none (placeholder text "Select a title…").

### Display
When a title is selected, render a `MudTable` of its chapters:

| Column | Source |
|---|---|
| Chapter # | `ChapterInfo.ChapterNumber` |
| Name | `ChapterInfo.Name` (shown as "Chapter {n}" if the name is empty or a default HandBrake placeholder) |
| Duration | `ChapterInfo.Duration` formatted as `h:mm:ss` |

A summary line below (or above) the table shows the cumulative duration of all chapters in the title.

If a title is selected but has no chapters, show a `MudAlert` info message: "This title has no chapter markers."

---

## 6. Open Questions

The following items need clarification before implementation begins:

1. ~~**Filesystem service path resolution**~~ — **Resolved:** all URLs use `root` (label) + `path` (relative) parameters. Physical paths are resolved server-side inside `ScanPage` via `IFilesystemService`. A `ResolvePhysicalPathAsync(rootLabel, relativePath)` method (or equivalent) will need to be added to `IFilesystemService` if one does not already exist.

2. ~~**Minimum-duration filter operator**~~ — **Resolved:** filter is `>= minimumSeconds` (inclusive).

3. ~~**Scan retry**~~ — **Resolved:** a **Retry** button re-runs the scan in-place. Additionally, changing the `root`/`path` query parameters (i.e. navigating back and selecting a different disc) naturally triggers a fresh scan via `OnInitializedAsync`.

4. ~~**Titles mode: chapter drill-down**~~ — **Resolved:** no drill-down. The filtered title table is the final output for Titles mode; rows are not interactive.

5. ~~**NavMenu entry**~~ — **Resolved:** `/scan` is **not** listed in the nav menu. It is only reachable by selecting a disc in the Filesystem browser.
