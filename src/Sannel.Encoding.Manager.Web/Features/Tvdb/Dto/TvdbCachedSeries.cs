namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;

/// <summary>
/// A previously looked-up TVDB series entry from the local cache.
/// </summary>
public sealed class TvdbCachedSeries
{
	public int SeriesId { get; init; }
	public string Name { get; init; } = string.Empty;
}
