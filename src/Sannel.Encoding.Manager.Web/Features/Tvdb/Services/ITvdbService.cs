using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

/// <summary>
/// Retrieves series and episode data from TheTVDB v4 API.
/// </summary>
public interface ITvdbService
{
	/// <summary>
	/// Returns all episodes for the given series using the specified order type,
	/// sorted by season then episode number.
	/// </summary>
	Task<IReadOnlyList<TvdbEpisode>> GetEpisodesAsync(
		int seriesId,
		TvdbEpisodeOrderType orderType = TvdbEpisodeOrderType.Default,
		CancellationToken ct = default);

	/// <summary>
	/// Returns the series name for the given series ID, or null if not found.
	/// </summary>
	Task<string?> GetSeriesNameAsync(int seriesId, CancellationToken ct = default);

	/// <summary>
	/// Returns all series previously fetched from TVDB that are stored in the local cache,
	/// ordered by name.
	/// </summary>
	Task<IReadOnlyList<TvdbCachedSeries>> GetCachedSeriesAsync(CancellationToken ct = default);
}
