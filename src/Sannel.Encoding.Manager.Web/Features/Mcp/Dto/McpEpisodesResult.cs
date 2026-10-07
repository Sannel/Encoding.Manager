using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Result of <c>tvdb_get_episodes</c>.</summary>
public class McpEpisodesResult
{
	public required int SeriesId { get; init; }

	public string? SeriesName { get; init; }

	public required string OrderType { get; init; }

	public required IReadOnlyList<TvdbEpisode> Episodes { get; init; }
}
