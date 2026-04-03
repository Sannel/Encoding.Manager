namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;

public class TvdbSeriesCache
{
	public int SeriesId { get; set; }
	public string? Name { get; set; }
	/// <summary>The original language code of the series (e.g. "eng", "jpn").</summary>
	public string? OriginalLanguage { get; set; }
	public DateTimeOffset CachedAt { get; set; }
}
