# Plan: TVDB Naming Panel with Cascade

## TL;DR
Add a naming panel below both Titles and Chapters mode views. Each row represents a title or chapter segment and has: a free-text name, season dropdown, and episode dropdown populated from TheTVDB v4 API. A "Cascade" button fills subsequent rows by incrementing the episode/season from the first filled row. API key stored in appsettings/user-secrets.

---

## Phase 1 — TVDB Feature Slice

**1. `Features/Tvdb/Options/TvdbOptions.cs`**
- `ApiKey` (string, from user-secrets)
- `BaseUrl` (string, default `https://api4.thetvdb.com/v4`)

**2. `Features/Tvdb/Dto/TvdbEpisode.cs`**
- `int SeasonNumber`, `int EpisodeNumber`, `string Name`

**3. `Features/Tvdb/Services/ITvdbService.cs`**
- `Task<IReadOnlyList<TvdbEpisode>> GetEpisodesAsync(int seriesId, CancellationToken ct)`

**4. `Features/Tvdb/Services/TvdbService.cs`**
- Injects `HttpClient` (typed client) and `IOptions<TvdbOptions>`
- On first call: POST `/login` with `{"apikey": "..."}` to get bearer token; cache it in `_token`
- Paginate GET `/series/{id}/episodes/official?page=0,1,…` until `links.next` is null
- Return flat `List<TvdbEpisode>` sorted by (SeasonNumber, EpisodeNumber)

**5. `appsettings.json`** — Add `"Tvdb": { "BaseUrl": "https://api4.thetvdb.com/v4", "ApiKey": "" }`

**6. `Program.cs`**
- `builder.Services.Configure<TvdbOptions>(builder.Configuration.GetSection("Tvdb"))`
- `builder.Services.AddHttpClient<ITvdbService, TvdbService>()`

---

## Phase 2 — Naming Panel Component

**7. `Features/Scan/Dto/NamingItem.cs`**
- `record NamingItem(string Label)` — passed from each mode view to NamingPanel

**8. `Features/Scan/Components/NamingPanel.razor` + `.razor.cs`**

Razor: collapsible `MudExpansionPanel` titled "Episode Naming"
- Row 1 controls: `MudTextField` "Show ID" + `MudButton` "Load from TVDB" + loading spinner + error chip
- `MudButton` "Cascade" (disabled if no rows have season+episode set)
- `MudTable` with columns: **Item**, **Name** (MudTextField), **Season** (MudSelect&lt;int?&gt;), **Episode** (MudSelect&lt;TvdbEpisode?&gt;)

Code-behind (private state):
- `private record NamingRow(string Label) { public string Name; public int? Season; public TvdbEpisode? Episode; }`
- `_rows`: `List<NamingRow>` rebuilt when `Items` parameter changes (`OnParametersSet`)
- `_showId`, `_isLoading`, `_errorMessage`, `_allEpisodes`, `_seasons`
- `_allEpisodes`: `IReadOnlyList<TvdbEpisode>` — loaded from TVDB
- `_seasons`: `IReadOnlyList<int>` derived from `_allEpisodes`
- `EpisodesForSeason(int season)` helper method
- `OnEpisodeSelected(NamingRow row, TvdbEpisode? ep)` — sets `row.Episode = ep`, auto-fills `row.Name = ep?.Name`
- `OnCascadeClicked()`:
  1. Find first row where `Season != null && Episode != null`
  2. Collect all episodes ordered by (SeasonNumber, EpisodeNumber) starting from found episode (exclusive)
  3. For each subsequent row, assign next episode and auto-fill Name

Parameters:
- `[Parameter] public required IReadOnlyList<NamingItem> Items { get; set; }`
- `[Inject] private ITvdbService TvdbService { get; set; }`
- `[Inject] private ISnackbar Snackbar { get; set; }`

---

## Phase 3 — Wire into Mode Views

**9. `TitlesModeView.razor` + `.razor.cs`**
- After the titles table, add `<NamingPanel Items="@NamingItems" />`
- `NamingItems` computed property: `FilteredTitles.Select(t => new NamingItem($"Title {t.TitleNumber} — {FormatDuration(t.Duration)}")).ToList()`
- Add `@using Sannel.Encoding.Manager.Web.Features.Scan.Dto` to razor

**10. `ChaptersModeView.razor` + `.razor.cs`**
- After segments table, inside the `@if (_selectedTitle is not null)` block: `<NamingPanel Items="@NamingItems" />`
- `NamingItems` computed property: `Segments.Select(s => new NamingItem($"Segment {s.SegmentNumber} (Ch {s.StartChapter}–{s.EndChapter})")).ToList()`
- `_rows` in NamingPanel must reset when `Items` changes (cover: title selection → new segments)

---

## Relevant Files
- `src/Sannel.Encoding.Manager.Web/Program.cs` — register typed HttpClient + TvdbService, TvdbOptions
- `src/Sannel.Encoding.Manager.Web/appsettings.json` — add Tvdb section
- `src/Sannel.Encoding.Manager.Web/Features/Scan/Components/TitlesModeView.razor` + `.razor.cs`
- `src/Sannel.Encoding.Manager.Web/Features/Scan/Components/ChaptersModeView.razor` + `.razor.cs`
- New files: `Features/Tvdb/Options/TvdbOptions.cs`, `Features/Tvdb/Dto/TvdbEpisode.cs`, `Features/Tvdb/Services/ITvdbService.cs`, `Features/Tvdb/Services/TvdbService.cs`, `Features/Scan/Dto/NamingItem.cs`, `Features/Scan/Components/NamingPanel.razor` + `.razor.cs`

---

## Verification
1. Build succeeds with `dotnet build` — 0 errors
2. All 27 tests still pass with `dotnet test`
3. Manually: enter a valid TVDB series ID → click "Load from TVDB" → seasons/episodes populate in rows
4. Manually: fill Season+Episode on row 1 → click "Cascade" → rows 2+ get S1E2, S1E3, etc. Names auto-fill from TVDB
5. Manually: switch title in Chapters mode → NamingPanel rows reset to match new segment count
6. TVDB section missing ApiKey → graceful error shown in UI (not a crash)

---

## Decisions
- TVDB API key: appsettings/user-secrets only (no UI input)
- No auto-fill on load — full manual per-row selection; Cascade is the only bulk-fill mechanism
- Cascade increments episode within season; crosses to next season (sorted by SeasonNumber then EpisodeNumber) when current season is exhausted
- On episode dropdown selection, Name auto-fills from episode name (user can still edit)
- NamingPanel is a sub-component used by both mode views (not owned by ScanPage) so it naturally resets when the parent re-renders with new Items
