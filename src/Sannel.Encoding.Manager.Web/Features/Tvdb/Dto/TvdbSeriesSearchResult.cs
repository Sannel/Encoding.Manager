namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

/// <summary>
/// A single series result returned by a TVDB name search.
/// </summary>
public sealed class TvdbSeriesSearchResult
{
	public int SeriesId { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Year { get; init; }
	public string? Overview { get; init; }
}
