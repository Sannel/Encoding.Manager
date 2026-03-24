namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;

public class TvdbSeriesCache
{
	public int SeriesId { get; set; }
	public string? Name { get; set; }
	public DateTimeOffset CachedAt { get; set; }
}
