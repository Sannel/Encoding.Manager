# Interlace Detection

The Scan page and the MCP tools report, for every disc title and media file, whether it is **interlaced** and which
preset that suggests. Nothing is applied automatically — the verdict pre-fills each track's preset selector, and MCP
clients receive a `recommendedPreset` per title / file.

| Source | How it is checked |
|---|---|
| DVD titles | Always reported **interlaced** (no probe). |
| Blu-ray titles | HandBrake's scan flag (`InterlaceDetected`) **plus** an ffmpeg `idet` probe of the title's playlist. If HandBrake saw combing but ffmpeg did not, the title is reported **mixed** (decomb is the safe choice). |
| Media files (folder / file selections) | ffmpeg `idet` probe of the file. |

Verdicts: `interlaced`, `telecined`, `mixed`, `progressive`, `pending` (probe still running) and `unknown`.
Interlaced, telecined and mixed suggest `Presets:InterlacedPresetLabel`; progressive suggests
`Presets:ProgressivePresetLabel`.

Probes run in the background after a disc scan (or when a folder is listed), at most `MaxConcurrentProbes` at a time,
and are cached in the database (`InterlaceProbeCache`) until the file — or the Blu-ray playlist file — changes. Expect
roughly 5–20 seconds per Blu-ray title and 2–8 seconds per SD file the first time; cached results are instant.

## Configuration

```json
"Interlace": {
  "Enabled": true,
  "FfmpegPath": null,
  "SamplePoints": [ 0.10, 0.45, 0.80 ],
  "FramesPerSample": 500,
  "ProbeTimeoutSeconds": 120,
  "MaxConcurrentProbes": 1,
  "InterlacedThresholdPercent": 10,
  "TelecineThresholdPercent": 10
},
"Presets": {
  "InterlacedPresetLabel": "4K AV1 Decomb",
  "ProgressivePresetLabel": "4K AV1"
}
```

| Setting | Meaning |
|---|---|
| `Enabled` | Turn the ffmpeg probes off (DVDs are still interlaced; Blu-ray titles use HandBrake's flag; files report `unknown`). |
| `FfmpegPath` | ffmpeg executable. `null` uses `ffmpeg` from `PATH`. |
| `SamplePoints` | Where in each title / file frames are sampled (fractions of its duration). |
| `FramesPerSample` | Frames decoded per sample point. |
| `InterlacedThresholdPercent` | Interlaced frames at or above this share → interlaced. |
| `TelecineThresholdPercent` | Frames with a repeated field at or above this share → telecined. |
| `Presets:*PresetLabel` | Preset labels suggested for interlaced / progressive sources (must match labels on the Presets page). |

## Installing ffmpeg

The probe needs an ffmpeg built with **libbluray** (the `bluray:` protocol) to read Blu-ray playlists. Check with:

```
ffmpeg -hide_banner -protocols
```

`bluray` must appear under *Input*. The server logs at startup which ffmpeg it found and whether Blu-ray probing is
available.

- **Docker / Linux:** the Docker image installs Debian's `ffmpeg`, which includes libbluray. On other Linux installs,
  `apt install ffmpeg` (or your distribution's equivalent).
- **Windows:** install a **"full"** ffmpeg build — for example the *full* build from gyan.dev or a *gpl* build from
  BtbN — extract it (e.g. to `C:\ffmpeg`) and set `"Interlace": { "FfmpegPath": "C:\\ffmpeg\\bin\\ffmpeg.exe" }`.
  "Essentials" builds do not include libbluray. Blu-ray probing does not need Java.

If ffmpeg is missing or has no libbluray, nothing fails: Blu-ray titles fall back to HandBrake's flag
(`interlaceSource: "handbrake"`) and media files report `unknown`.
