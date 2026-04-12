namespace Sannel.Encoding.Manager.Web.Features.Omdb.Dto;

/// <summary>
/// Movie metadata from OMDB (Open Movie Database).
/// </summary>
public class OmdbMovie
{
	/// <summary>
	/// IMDb ID (e.g. "tt1234567").
	/// </summary>
	public string ImdbId { get; set; } = string.Empty;

	/// <summary>
	/// Movie title.
	/// </summary>
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Release year as a string (e.g. "2020").
	/// </summary>
	public string Year { get; set; } = string.Empty;

	/// <summary>
	/// Plot summary.
	/// </summary>
	public string Plot { get; set; } = string.Empty;

	/// <summary>
	/// Comma-separated list of genres.
	/// </summary>
	public string Genres { get; set; } = string.Empty;
}
