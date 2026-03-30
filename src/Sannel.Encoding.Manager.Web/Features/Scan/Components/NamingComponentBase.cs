using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Settings.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

/// <summary>
/// Base component that provides TVDB episode loading, per-row naming state, and cascade logic
/// for mode views that embed naming columns directly in their tables.
/// </summary>
public abstract class NamingComponentBase : ComponentBase
{
	[Inject]
	private ITvdbService TvdbService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IEncodeQueueService EncodeQueueService { get; set; } = default!;

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private ISettingsService SettingsService { get; set; } = default!;

	protected sealed class NamingRowData
	{
		public string Name { get; set; } = string.Empty;
		public int? Season { get; set; }
		public TvdbEpisode? Episode { get; set; }
	}

	protected string _showId = string.Empty;
	protected bool _isTvdbLoading;
	protected string? _tvdbErrorMessage;
	protected string? _seriesName;
	protected TvdbEpisodeOrderType _episodeOrderType = TvdbEpisodeOrderType.Default;
	protected IReadOnlyList<TvdbEpisode> _allEpisodes = [];
	protected IReadOnlyList<int> _seasons = [];
	protected readonly Dictionary<int, NamingRowData> _namingRows = [];

	/// <summary>Available HandBrake presets loaded from the database.</summary>
	protected IReadOnlyList<EncodingPreset> _presets = [];

	/// <summary>The preset label selected on the scan page (applied to all tracks when queuing).</summary>
	protected string? _selectedPresetLabel;

	protected override async Task OnInitializedAsync()
	{
		this._presets = await this.PresetService.GetPresetsAsync();
	}

	protected bool CanCascade =>
		this._namingRows.Values.Any(r => r.Season is not null && r.Episode is not null);

	protected NamingRowData GetNamingRow(int key)
	{
		if (!this._namingRows.TryGetValue(key, out var row))
		{
			row = new NamingRowData();
			this._namingRows[key] = row;
		}

		return row;
	}

	/// <summary>
	/// Filters tracks with an empty OutputName, builds one disk-level queue item,
	/// stamps AudioDefault from settings, persists, and shows a snackbar summary.
	/// </summary>
	protected async Task AddDiskToQueueAsync(string discPath, string mode, IReadOnlyList<EncodeTrackConfig> tracks)
	{
		var toAdd = tracks.Where(t => !string.IsNullOrWhiteSpace(t.OutputName)).ToList();
		if (toAdd.Count == 0)
		{
			this.Snackbar.Add("No tracks to queue — all track names are empty.", Severity.Warning);
			return;
		}

		// Stamp the globally selected preset on every track
		foreach (var track in toAdd)
		{
			track.PresetLabel = this._selectedPresetLabel;
		}

		var settings = await this.SettingsService.GetSettingsAsync();
		var tvdbId = int.TryParse(this._showId.Trim(), out var parsedId) ? parsedId : (int?)null;
		var item = new EncodeQueueItem
		{
			DiscPath = discPath,
			Mode = mode,
			TvdbShowName = this._seriesName,
			TvdbId = tvdbId,
			TracksJson = JsonSerializer.Serialize(toAdd),
			AudioDefault = settings.AudioDefault,
		};

		await this.EncodeQueueService.AddItemAsync(item);
		this.Snackbar.Add($"Disc added to queue with {toAdd.Count} track(s).", Severity.Success);
	}

	protected IReadOnlyList<TvdbEpisode> EpisodesForSeason(int? season)
	{
		if (season is null)
		{
			return [];
		}

		return this._allEpisodes
			.Where(e => e.SeasonNumber == season)
			.OrderBy(e => e.EpisodeNumber)
			.ToList();
	}

	protected async Task OnLoadFromTvdbClicked()
	{
		if (!int.TryParse(this._showId.Trim(), out var seriesId))
		{
			this._tvdbErrorMessage = "Show ID must be a whole number (e.g. 73739).";
			return;
		}

		this._isTvdbLoading = true;
		this._tvdbErrorMessage = null;

		try
		{
			this._seriesName = await this.TvdbService.GetSeriesNameAsync(seriesId);
			var episodes = await this.TvdbService.GetEpisodesAsync(seriesId, this._episodeOrderType);
			this._allEpisodes = episodes;
			this._seasons = episodes
				.Select(e => e.SeasonNumber)
				.Distinct()
				.OrderBy(s => s)
				.ToList();

			this.Snackbar.Add($"Loaded {episodes.Count} episode(s) from TVDB.", Severity.Success);
		}
		catch (Exception ex)
		{
			this._tvdbErrorMessage = $"Failed to load from TVDB: {ex.Message}";
			this._seriesName = null;
			this._allEpisodes = [];
			this._seasons = [];
		}
		finally
		{
			this._isTvdbLoading = false;
		}
	}

	protected void OnSeasonChanged(int key, int? value)
	{
		var row = this.GetNamingRow(key);
		row.Season = value;
		row.Episode = null;
	}

	protected void OnEpisodeSelected(int key, TvdbEpisode? ep)
	{
		var row = this.GetNamingRow(key);
		row.Episode = ep;
		if (ep is not null)
		{
			row.Name = ep.Name;
		}
	}

	protected virtual string GetFallbackAutoName(int key) => string.Empty;

	protected void ApplyEpisodeName(int key)
	{
		var row = this.GetNamingRow(key);
		if (row.Episode is not null)
		{
			row.Name = row.Episode.Name;
		}
		else
		{
			var fallback = this.GetFallbackAutoName(key);
			if (!string.IsNullOrEmpty(fallback))
			{
				row.Name = fallback;
			}
		}
	}

	protected void ApplyAllEpisodeNames()
	{
		foreach (var key in this._namingRows.Keys)
		{
			this.ApplyEpisodeName(key);
		}
	}

	protected void ClearNamingRow(int key)
	{
		if (this._namingRows.TryGetValue(key, out var row))
		{
			row.Name = string.Empty;
			row.Season = null;
			row.Episode = null;
		}
	}

	protected void OnEpisodeOrderTypeChanged(TvdbEpisodeOrderType type)
	{
		this._episodeOrderType = type;
		// Clear loaded episode data so the user reloads with the new order
		this._allEpisodes = [];
		this._seasons = [];
		this._tvdbErrorMessage = null;
	}

	protected void CascadeRows(IReadOnlyList<int> orderedKeys)
	{
		var firstIndex = -1;
		for (var i = 0; i < orderedKeys.Count; i++)
		{
			var r = this.GetNamingRow(orderedKeys[i]);
			if (r.Season is not null && r.Episode is not null)
			{
				firstIndex = i;
				break;
			}
		}

		if (firstIndex < 0)
		{
			return;
		}

		var firstRow = this.GetNamingRow(orderedKeys[firstIndex]);
		var sorted = this._allEpisodes
			.OrderBy(e => e.SeasonNumber)
			.ThenBy(e => e.EpisodeNumber)
			.ToList();

		var startIndex = sorted.FindIndex(e =>
			e.SeasonNumber == firstRow.Episode!.SeasonNumber
			&& e.EpisodeNumber == firstRow.Episode.EpisodeNumber);

		if (startIndex < 0)
		{
			return;
		}

		var nextEpIdx = startIndex + 1;
		for (var i = firstIndex + 1; i < orderedKeys.Count && nextEpIdx < sorted.Count; i++, nextEpIdx++)
		{
			var ep = sorted[nextEpIdx];
			var row = this.GetNamingRow(orderedKeys[i]);
			row.Season = ep.SeasonNumber;
			row.Episode = ep;
			row.Name = ep.Name;
		}
	}
}
