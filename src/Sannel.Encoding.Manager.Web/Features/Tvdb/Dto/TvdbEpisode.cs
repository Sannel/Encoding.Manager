namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

/// <summary>
/// A single episode from TheTVDB.
/// </summary>
public record TvdbEpisode
{
	public int SeasonNumber { get; init; }
	public int EpisodeNumber { get; init; }
	public string Name { get; init; } = string.Empty;
}
