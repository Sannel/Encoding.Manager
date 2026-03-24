using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class NamingPanel : ComponentBase
{
	[Parameter]
	public required IReadOnlyList<NamingItem> Items { get; set; }

	[Inject]
	private ITvdbService TvdbService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private sealed class NamingRow
	{
		public string Label { get; init; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int? Season { get; set; }
		public TvdbEpisode? Episode { get; set; }
	}

	private List<NamingRow> _rows = [];
	private string _showId = string.Empty;
	private bool _isLoading;
	private string? _errorMessage;
	private IReadOnlyList<TvdbEpisode> _allEpisodes = [];
	private IReadOnlyList<int> _seasons = [];

	private bool CanCascade =>
		this._rows.Any(r => r.Season is not null && r.Episode is not null);

	protected override void OnParametersSet()
	{
		var newLabels = this.Items.Select(i => i.Label).ToList();
		var oldLabels = this._rows.Select(r => r.Label).ToList();

		if (newLabels.SequenceEqual(oldLabels))
		{
			return;
		}

		this._rows = this.Items
			.Select(i => new NamingRow { Label = i.Label })
			.ToList();
	}

	private IReadOnlyList<TvdbEpisode> EpisodesForSeason(int? season)
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

	private async Task OnLoadFromTvdbClicked()
	{
		if (!int.TryParse(this._showId.Trim(), out var seriesId))
		{
			this._errorMessage = "Show ID must be a whole number (e.g. 73739).";
			return;
		}

		this._isLoading = true;
		this._errorMessage = null;

		try
		{
			var episodes = await this.TvdbService.GetEpisodesAsync(seriesId);
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
			this._errorMessage = $"Failed to load from TVDB: {ex.Message}";
			this._allEpisodes = [];
			this._seasons = [];
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private void OnSeasonChanged(NamingRow row, int? value)
	{
		row.Season = value;
		row.Episode = null;
	}

	private void OnEpisodeSelected(NamingRow row, TvdbEpisode? ep)
	{
		row.Episode = ep;
		if (ep is not null)
		{
			row.Name = ep.Name;
		}
	}

	private void OnCascadeClicked()
	{
		var firstFilledIndex = this._rows.FindIndex(r => r.Season is not null && r.Episode is not null);
		if (firstFilledIndex < 0)
		{
			return;
		}

		var firstRow = this._rows[firstFilledIndex];
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

		var nextEpisodeIndex = startIndex + 1;
		for (var i = firstFilledIndex + 1; i < this._rows.Count && nextEpisodeIndex < sorted.Count; i++, nextEpisodeIndex++)
		{
			var ep = sorted[nextEpisodeIndex];
			this._rows[i].Season = ep.SeasonNumber;
			this._rows[i].Episode = ep;
			this._rows[i].Name = ep.Name;
		}
	}
}
