namespace Sannel.Encoding.Manager.Web.Features.Omdb.Dto;

/// <summary>
/// A single movie result returned by an OMDb title search.
/// </summary>
public sealed class OmdbSearchResult
{
	public string ImdbId { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string Year { get; init; } = string.Empty;
}
