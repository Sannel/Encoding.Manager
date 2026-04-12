using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Omdb.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Utilities;
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
	private IOmdbService OmdbService { get; set; } = default!;

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
		public string? Resolution { get; set; }
	}

	protected string _showId = string.Empty;
	protected bool _isTvdbLoading;
	protected string? _tvdbErrorMessage;
	protected string? _seriesName;
	protected TvdbEpisodeOrderType _episodeOrderType = TvdbEpisodeOrderType.Default;
	protected IReadOnlyList<TvdbEpisode> _allEpisodes = [];
	protected IReadOnlyList<int> _seasons = [];
	protected readonly Dictionary<int, NamingRowData> _namingRows = [];

	/// <summary>Movie search/lookup fields (used in Movie mode).</summary>
	protected string _movieTitle = string.Empty;
	protected bool _isOmdbLoading;
	protected string? _omdbErrorMessage;
	protected string? _movieName;
	protected string? _movieYear;
	protected string? _movieGenres;

	/// <summary>Available HandBrake presets loaded from the database.</summary>
	protected IReadOnlyList<EncodingPreset> _presets = [];

	/// <summary>Available video resolutions for dropdown selection.</summary>
	protected IReadOnlyList<string> _availableResolutions = ResolutionDetector.GetAvailableResolutions();

	/// <summary>The preset label selected on the scan page (applied to all tracks when queuing).</summary>
	protected string? _selectedPresetLabel;

	/// <summary>Previously looked-up TVDB series from the local cache, for quick re-selection.</summary>
	protected IReadOnlyList<TvdbCachedSeries> _cachedSeries = [];

	/// <summary>The series currently selected in the cached-show dropdown.</summary>
	protected TvdbCachedSeries? _selectedCachedShow;

	protected override async Task OnInitializedAsync()
	{
		this._presets = await this.PresetService.GetPresetsAsync();
		this._cachedSeries = await this.TvdbService.GetCachedSeriesAsync();
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
	protected async Task AddDiskToQueueAsync(string discPath, string? discRootLabel, string mode, IReadOnlyList<EncodeTrackConfig> tracks)
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
			DiscRootLabel = discRootLabel,
			Mode = mode,
			TvdbShowName = this._seriesName,
			TvdbId = tvdbId,
			TracksJson = JsonSerializer.Serialize(toAdd),
			AudioDefault = settings.AudioDefault,
		};

		await this.EncodeQueueService.AddItemAsync(item);
		var subject = string.Equals(mode, "Files", StringComparison.OrdinalIgnoreCase) ? "Folder" : "Disc";
		this.Snackbar.Add($"{subject} added to queue with {toAdd.Count} track(s).", Severity.Success);
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

	protected async Task OnCachedShowSelected(TvdbCachedSeries? series)
	{
		this._selectedCachedShow = series;
		if (series is null)
		{
			return;
		}

		this._showId = series.SeriesId.ToString();
		await this.OnLoadFromTvdbClicked();
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
			row.Resolution = null;
		}
	}

	protected void OnResolutionChanged(int key, string? resolution)
	{
		var row = this.GetNamingRow(key);
		row.Resolution = resolution;
	}

	protected void OnEpisodeOrderTypeChanged(TvdbEpisodeOrderType type)
	{
		this._episodeOrderType = type;
		// Clear loaded episode data so the user reloads with the new order
		this._allEpisodes = [];
		this._seasons = [];
		this._tvdbErrorMessage = null;
	}

	protected async Task OnLoadFromOmdbClicked()
	{
		if (string.IsNullOrWhiteSpace(this._movieTitle))
		{
			this._omdbErrorMessage = "Enter a movie title to search.";
			return;
		}

		this._isOmdbLoading = true;
		this._omdbErrorMessage = null;

		try
		{
			var movie = await this.OmdbService.SearchMovieAsync(this._movieTitle);
			if (movie is null)
			{
				this._omdbErrorMessage = "No movie found with that title.";
				this._movieName = null;
				this._movieYear = null;
				this._movieGenres = null;
				return;
			}

			this._movieName = movie.Title;
			this._movieYear = movie.Year;
			this._movieGenres = movie.Genres;

			this.Snackbar.Add($"Loaded '{movie.Title}' ({movie.Year}) from OMDb.", Severity.Success);
		}
		catch (Exception ex)
		{
			this._omdbErrorMessage = $"Failed to load from OMDb: {ex.Message}";
			this._movieName = null;
			this._movieYear = null;
			this._movieGenres = null;
		}
		finally
		{
			this._isOmdbLoading = false;
		}
	}

	protected void ApplyMovieName(int key)
	{
		var row = this.GetNamingRow(key);
		if (!string.IsNullOrEmpty(this._movieName))
		{
			row.Name = this._movieName;
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

	protected void ApplyAllMovieNames()
	{
		foreach (var key in this._namingRows.Keys)
		{
			this.ApplyMovieName(key);
		}
	}

	protected void ApplyDetectedResolution(int key, int width, int height)
	{
		var resolution = ResolutionDetector.DetectResolution(width, height);
		this.OnResolutionChanged(key, resolution);
	}

	protected void ApplyAllDetectedResolutions(int width, int height)
	{
		var resolution = ResolutionDetector.DetectResolution(width, height);
		foreach (var key in this._namingRows.Keys)
		{
			this.OnResolutionChanged(key, resolution);
		}
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
