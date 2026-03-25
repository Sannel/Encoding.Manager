namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;

public class TvdbEpisodeCache
{
	public int SeriesId { get; set; }
	/// <summary>TVDB season type path (official, dvd, absolute, streaming).</summary>
	public string OrderType { get; set; } = "official";
	public int SeasonNumber { get; set; }
	public int EpisodeNumber { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTimeOffset CachedAt { get; set; }
}
