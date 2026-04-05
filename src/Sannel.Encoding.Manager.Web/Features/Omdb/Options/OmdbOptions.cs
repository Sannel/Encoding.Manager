namespace Sannel.Encoding.Manager.Web.Features.Omdb.Options;

/// <summary>
/// Configuration options for OMDB (Open Movie Database) API integration.
/// </summary>
public class OmdbOptions
{
	/// <summary>
	/// OMDb API key. If empty or null, OMDb features are disabled.
	/// </summary>
	public string? ApiKey { get; set; }

	/// <summary>
	/// OMDb API base URL. Defaults to https://www.omdbapi.com/ if not set.
	/// </summary>
	public string? BaseUrl { get; set; }
}
