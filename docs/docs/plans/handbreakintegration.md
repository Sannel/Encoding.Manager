# HandBrake Integration Plan

## Overview

Add a testable HandBrake CLI integration to the `Sannel.Encoding.Manager.Web` application. The integration wraps the `HandBrakeCLI` process, streams and parses its output (preferring JSON where available), and exposes a clean `IHandBrakeService` API that multiple feature slices can consume. Because it is shared across slices it lives in `Features/Utility/HandBrake/`.

---

## Confirmed Decisions

| Decision | Choice |
|---|---|
| Output parsing | Prefer HandBrake JSON output; fall back to text parsing for older versions |
| Runtime requirement | `HandBrakeCLI` must be installed — fail-fast on startup if absent |
| Minimum CLI version | Documented in README + runtime version check at startup; configurable via `MinimumVersion` in `appsettings.json` (default `10.1`) |
| DI lifetime | Singleton |
| Scan result persistence | Written to a configurable path (`appsettings.json` only) |
| Encode queue | Fire-and-forget — no job queue or tracking |
| Presets | Caller **must** pass a preset name string or a preset file path — no default; service throws `ArgumentException` if neither is supplied |
| Error surfacing | Return a typed result object with `IsSuccess`, `Error`, and `ErrorMessage` |
| Logging | `Information` level for normal stdout/stderr; `Error` for failures |
| Progress reporting | `IProgress<ProgressInfo>` callback on `EncodeAsync` |
| Timeout | None — rely solely on `CancellationToken` |
| Target platforms | Linux and Windows |

---

## Configuration (`appsettings.json`)

```json
"HandBrake": {
  "ExecutablePath": "",         // Leave empty to auto-detect from PATH
  "ScanOutputPath": "handbrake-scans",  // Relative to app root by default
  "MinimumVersion": "10.1"              // Minimum acceptable HandBrakeCLI version
}
```

- `ExecutablePath`: if empty the service searches `PATH` for `HandBrakeCLI` (Linux) or `HandBrakeCLI.exe` (Windows). On Linux the locator also probes for a Flatpak install (see Platform Considerations).
- `ScanOutputPath`: directory where scan result JSON files are written. **Relative paths are resolved relative to the app's content root** (`IWebHostEnvironment.ContentRootPath`). Absolute paths are used as-is. The directory will be created if it does not exist.
- `MinimumVersion`: the lowest `HandBrakeCLI` version the service will accept. Parsed as `System.Version`. If the detected CLI version is lower, the service throws `InvalidOperationException` at startup with a clear message showing both the required and detected versions.
- ~~`DefaultPreset`~~ — **removed**. Callers must always supply either `PresetName` or `PresetFilePath` on `HandBrakeJob`. `EncodeAsync` throws `ArgumentException` if neither is set.

---

## Public API

### `IHandBrakeService`

```csharp
public interface IHandBrakeService
{
    /// <summary>Scans the input file and returns track/title/stream metadata.</summary>
    Task<HandBrakeScanResult> ScanAsync(string inputPath, CancellationToken ct = default);

    /// <summary>Encodes the input according to the job spec, reporting progress via <paramref name="progress"/>.</summary>
    Task<HandBrakeEncodeResult> EncodeAsync(
        HandBrakeJob job,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken ct = default);

    /// <summary>Returns the detected HandBrakeCLI version string.</summary>
    string CliVersion { get; }
}
```

### Models / DTOs

**`HandBrakeJob`** — describes an encode request

| Property | Type | Description |
|---|---|---|
| `InputPath` | `string` | Absolute path to source file |
| `OutputPath` | `string` | Absolute path for output file |
| `PresetName` | `string?` | HandBrake preset name (e.g. `"Fast 1080p30"`) |
| `PresetFilePath` | `string?` | Path to a `.json` preset file (takes precedence over `PresetName` if both are set) |
| `AdditionalArgs` | `string?` | Raw extra CLI arguments appended verbatim — caller responsibility to sanitize |

**`HandBrakeScanResult`** — result of a `--scan` operation

| Property | Type | Description |
|---|---|---|
| `IsSuccess` | `bool` | Whether the scan completed without error |
| `Error` | `HandBrakeError?` | Structured error details if `IsSuccess` is false |
| `InputPath` | `string` | The file that was scanned |
| `ScanOutputPath` | `string` | Path to the persisted JSON file |
| `Titles` | `IReadOnlyList<TitleInfo>` | All detected titles |

**`TitleInfo`** — per-title metadata from a scan

| Property | Type | Description |
|---|---|---|
| `TitleNumber` | `int` | HandBrake title index |
| `Duration` | `TimeSpan` | Title duration |
| `VideoStreams` | `IReadOnlyList<VideoStreamInfo>` | Video tracks |
| `AudioTracks` | `IReadOnlyList<AudioTrackInfo>` | Audio tracks |
| `Subtitles` | `IReadOnlyList<SubtitleInfo>` | Subtitle tracks |
| `Chapters` | `IReadOnlyList<ChapterInfo>` | Chapter markers |
| `FrameRate` | `double` | Frames per second |
| `Resolution` | `(int Width, int Height)` | Video resolution |

**`HandBrakeEncodeResult`** — result of an encode

| Property | Type | Description |
|---|---|---|
| `IsSuccess` | `bool` | Whether encoding completed successfully |
| `Error` | `HandBrakeError?` | Structured error details if `IsSuccess` is false |
| `OutputPath` | `string` | Path of the encoded file |
| `ElapsedTime` | `TimeSpan` | Total wall-clock encode time |
| `AverageFps` | `double` | Average frames per second during encode |

**`ProgressInfo`** — incremental progress updates during encode

| Property | Type | Description |
|---|---|---|
| `Percent` | `double` | 0–100 completion |
| `CurrentPhase` | `string` | e.g. `"Encoding"`, `"Muxing"` |
| `CurrentFps` | `double` | Instantaneous frames-per-second |
| `AverageFps` | `double` | Running average FPS |
| `Eta` | `TimeSpan?` | Estimated time remaining |

**`HandBrakeError`** — structured failure details

| Property | Type | Description |
|---|---|---|
| `ExitCode` | `int` | `HandBrakeCLI` process exit code |
| `Message` | `string` | Human-readable error summary |
| `RawOutput` | `string` | Full stderr text captured from the process |

---

## Internal Components

### `HandBrakeOptions`

Options POCO bound from `"HandBrake"` configuration section.

### `IProcessRunner` / `ProcessRunner`

Thin abstraction over `System.Diagnostics.Process` that:
- Launches a process with given arguments.
- Exposes `IAsyncEnumerable<string>` streams for stdout and stderr separately.
- Accepts a `CancellationToken` to kill the process.
- Returns the exit code when the process exits.

This abstraction is what makes the service unit-testable without spawning real processes.

### `HandBrakeParser`

Stateless parsing class with no external dependencies.

- `ScanResult ParseScan(string jsonOrText)` — attempts JSON first; falls back to text parsing.
- `ProgressInfo? ParseProgressLine(string line)` — parses a single live progress line from stdout.
- `bool TryParseVersion(string output, out Version version)` — extracts and parses the version string as `System.Version`.

### `HandBrakeService`

Implements `IHandBrakeService`. On construction:
1. Calls `HandBrakeExecutableLocator` to resolve the binary and any prefix args (e.g. `flatpak run …` on Linux).
2. Runs the resolved command with `--version` to confirm it is reachable and to capture `CliVersion`.
3. Parses the detected version as `System.Version` and compares it against `HandBrakeOptions.MinimumVersion` (default `10.1`). Throws `InvalidOperationException` with a message like `"HandBrakeCLI 10.1 or higher is required but 9.x was found"` if the check fails.
4. Throws `InvalidOperationException` with a clear message if the executable cannot be found, the Flatpak app is not installed, or the version check returns a non-zero exit code.

`ScanAsync`:
1. Runs `HandBrakeCLI --input <path> --scan --json` (JSON flag) or `--scan` (text fallback).
2. Buffers the output and delegates to `HandBrakeParser.ParseScan`.
3. Writes the raw JSON to `ScanOutputPath/<inputFilename>-<timestamp>.json`.
4. Returns a `HandBrakeScanResult`.

`EncodeAsync`:
1. Validates that `HandBrakeJob.PresetName` or `HandBrakeJob.PresetFilePath` is set; throws `ArgumentException` if neither is provided.
2. Builds CLI arguments from `HandBrakeJob` (preset name or `--preset-import-file` for a file path, plus any `AdditionalArgs`). Prefix args from the locator (e.g. Flatpak) are prepended automatically.
3. Streams stdout line-by-line; each line is passed to `HandBrakeParser.ParseProgressLine` and forwarded to `progress` if non-null.
4. Returns a `HandBrakeEncodeResult` with timing statistics on success, or a failed result with `HandBrakeError` on non-zero exit.

> **Security note**: `AdditionalArgs` are passed verbatim. Any UI or API layer that accepts these from user input **must** validate/allowlist them before passing to the service. `InputPath` and `OutputPath` are resolved to absolute paths and validated to exist (input) and be writable (output directory) before the process is launched.

---

## Platform Considerations

| Platform | Executable name | Discovery strategy |
|---|---|---|
| Linux (native) | `HandBrakeCLI` | Search `PATH`; fallback to `/usr/bin/HandBrakeCLI` |
| Linux (Flatpak) | `flatpak run --command=HandBrakeCLI fr.handbrake.HandBrake` | Detected when native binary not found; confirmed by running `flatpak list --app` and checking for `fr.handbrake.HandBrake` |
| Windows | `HandBrakeCLI.exe` | Search `PATH`; fallback to `%ProgramFiles%\HandBrake\HandBrakeCLI.exe` |

### Linux Flatpak detection detail

`HandBrakeExecutableLocator` on Linux follows this resolution order:
1. Use `ExecutablePath` from config if non-empty.
2. Search each directory in `PATH` for `HandBrakeCLI`.
3. Check `/usr/bin/HandBrakeCLI`.
4. Run `flatpak list --app --columns=application` and look for `fr.handbrake.HandBrake`. If found, return the tuple `("flatpak", ["run", "--command=HandBrakeCLI", "fr.handbrake.HandBrake"])` so the runner launches `flatpak run --command=HandBrakeCLI fr.handbrake.HandBrake <args>` instead of a direct executable.
5. If none found, throw `InvalidOperationException` at startup.

The locator returns a `HandBrakeExecutable` record: `{ string Binary, IReadOnlyList<string> PrefixArgs }`. `ProcessRunner` prepends `PrefixArgs` before the actual HandBrake arguments so callers are unaware of the Flatpak indirection.

The auto-discovery logic is encapsulated in `HandBrakeExecutableLocator` so it can be tested independently per platform.

---

## Error Handling Strategy

- All errors return a result object (`HandBrakeScanResult` / `HandBrakeEncodeResult`) with `IsSuccess = false` and a populated `HandBrakeError`.
- The service does **not** throw for expected failures (e.g. non-zero CLI exit, parse failure). It **does** throw for programmer errors (null arguments, path not found before launching, misconfiguration).
- Startup fail-fast (`InvalidOperationException`) if `HandBrakeCLI` is not found or version check fails.

---

## Logging

All log entries use the category `Sannel.Encoding.Manager.Web.Features.Utility.HandBrake`.

| Event | Level |
|---|---|
| Service initialized, CLI version detected, version meets minimum requirement | `Information` |
| CLI version below `MinimumVersion` | `Error` |
| Scan started / completed | `Information` |
| Encode started / completed | `Information` |
| Progress updates | `Debug` |
| Scan/encode result written to file | `Information` |
| Non-zero exit code or parse failure | `Error` |
| Full stderr on failure | `Error` |

---

## Implementation Steps

### Step 1 — Discovery

- [ ] Install `HandBrakeCLI` in the dev environment.
- [ ] Capture sample `--scan --json` output and plain-text `--scan` output from a test file — save to `tests/HandBrakeParser.Tests/TestData/`.
- [ ] Capture plain-text and JSON encode progress lines for parser test data.
- [ ] Confirm minimum supported HandBrake version is set to `10.1` as the default in `HandBrakeOptions`; capture sample `HandBrakeCLI --version` output for parser tests.

### Step 2 — Models and options

- [ ] Create `HandBrakeModels.cs` with all DTOs listed above.
- [ ] Create `HandBrakeOptions.cs` with the three configuration properties.
- [ ] Bind options in `Program.cs` via `builder.Services.Configure<HandBrakeOptions>(builder.Configuration.GetSection("HandBrake"))`.
- [ ] Add `HandBrake` section with sensible defaults to `appsettings.json` and `appsettings.Development.json`.

### Step 3 — Process abstraction

- [ ] Define `IProcessRunner` interface.
- [ ] Implement `ProcessRunner` wrapping `System.Diagnostics.Process` with async stdout/stderr streaming and `CancellationToken` support.

### Step 4 — Parser

- [ ] Implement `HandBrakeParser.ParseScan` for JSON path (using `System.Text.Json`).
- [ ] Implement text-parsing fallback in `HandBrakeParser` using regex/state machine.
- [ ] Implement `ParseProgressLine` for live encode progress.
- [ ] Implement `TryParseVersion`.

### Step 5 — Service

- [ ] Implement `HandBrakeExecutableLocator` for Linux + Windows discovery.
- [ ] Implement `HandBrakeService` composing `IProcessRunner`, `HandBrakeParser`, and `HandBrakeOptions`.
- [ ] Register `IHandBrakeService` as singleton in `Program.cs`.

### Step 6 — Unit tests

- [ ] `HandBrakeParserTests` — one test per sample file (JSON scan, text scan, progress line variants, version string).
- [ ] `HandBrakeServiceTests` — mock `IProcessRunner` to cover: successful scan, failed scan (non-zero exit), successful encode with progress callbacks, cancelled encode, missing executable.
- [ ] `HandBrakeExecutableLocatorTests` — cover PATH-found, fallback-found, and not-found cases (use environment variable injection).

### Step 7 — Integration verification

- [ ] Run `HandBrakeCLI --version` against real install and confirm version check passes.
- [ ] Run `ScanAsync` against a test video file; verify scan result and output JSON file on disk.
- [ ] Run `EncodeAsync` against a short clip; verify progress callbacks fire and output file is produced.
- [ ] Test cancellation: cancel a long encode mid-way and confirm process is killed cleanly.

### Step 8 — Documentation

- [ ] Add runtime dependency section to repository README (Linux: `sudo apt install handbrake-cli`, Windows: download from handbrake.fr).
- [ ] Document minimum HandBrake version.
- [ ] Update this plan to mark steps complete as work progresses.

---

## Files to Create or Modify

| File | Action |
|---|---|
| `Features/Utility/HandBrake/IHandBrakeService.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeService.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeModels.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeOptions.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeParser.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeProcessRunner.cs` | Create |
| `Features/Utility/HandBrake/HandBrakeExecutableLocator.cs` | Create |
| `Program.cs` | Modify — register options and singleton service |
| `appsettings.json` | Modify — add `HandBrake` section |
| `appsettings.Development.json` | Modify — add dev overrides |
| `tests/Sannel.Encoding.Manager.HandBrake.Tests/HandBrakeParserTests.cs` | Create |
| `tests/Sannel.Encoding.Manager.HandBrake.Tests/HandBrakeServiceTests.cs` | Create |
| `tests/Sannel.Encoding.Manager.HandBrake.Tests/HandBrakeExecutableLocatorTests.cs` | Create |
| `tests/Sannel.Encoding.Manager.HandBrake.Tests/TestData/` | Create — sample CLI output files |
| `README.md` | Modify — runtime dependency section |

