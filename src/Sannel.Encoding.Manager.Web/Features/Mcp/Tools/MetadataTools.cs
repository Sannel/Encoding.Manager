using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Omdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Omdb.Services;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools for TheTVDB and OMDb lookups used to name output files.</summary>
[McpServerToolType]
public class MetadataTools
{
	private readonly ITvdbService _tvdbService;
	private readonly IOmdbService _omdbService;

	public MetadataTools(ITvdbService tvdbService, IOmdbService omdbService)
	{
		this._tvdbService = tvdbService;
		this._omdbService = omdbService;
	}

	[McpServerTool(Name = "tvdb_list_cached_series", ReadOnly = true, Idempotent = true)]
	[Description("Lists TV series previously looked up on TheTVDB (id and name), for quick reuse.")]
	public Task<IReadOnlyList<TvdbCachedSeries>> ListCachedSeriesAsync(CancellationToken ct) =>
		this._tvdbService.GetCachedSeriesAsync(ct);

	[McpServerTool(Name = "tvdb_get_episodes", ReadOnly = true, Idempotent = true)]
	[Description("Returns a TV series' name and every episode (season, episode number, name) from TheTVDB. Episode names become output names; season/episode numbers go on each track.")]
	public async Task<McpEpisodesResult> GetEpisodesAsync(
		[Description("TheTVDB series id (e.g. 73739).")] int seriesId,
		[Description("Episode order: Default, Dvd, Bluray or Streaming. Disc releases often match Dvd order.")] TvdbEpisodeOrderType orderType = TvdbEpisodeOrderType.Default,
		CancellationToken ct = default)
	{
		try
		{
			var name = await this._tvdbService.GetSeriesNameAsync(seriesId, ct);
			var episodes = await this._tvdbService.GetEpisodesAsync(seriesId, orderType, ct);
			return new McpEpisodesResult
			{
				SeriesId = seriesId,
				SeriesName = name,
				OrderType = orderType.ToString(),
				Episodes = episodes,
			};
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
		{
			throw new McpException($"TheTVDB lookup failed: {ex.Message}");
		}
	}

	[McpServerTool(Name = "omdb_search_movie", ReadOnly = true, Idempotent = true)]
	[Description("Searches OMDb for a movie by title and returns its title, year and genres (first match).")]
	public async Task<OmdbMovie> SearchMovieAsync(
		[Description("Movie title to search for.")] string title,
		CancellationToken ct = default)
	{
		if (!this._omdbService.IsConfigured)
		{
			throw new McpException("OMDb is not configured on this server (Omdb:ApiKey).");
		}

		try
		{
			return await this._omdbService.SearchMovieAsync(title, ct)
				?? throw new McpException($"No movie found matching \"{title}\".");
		}
		catch (HttpRequestException ex)
		{
			throw new McpException($"OMDb lookup failed: {ex.Message}");
		}
	}
}
