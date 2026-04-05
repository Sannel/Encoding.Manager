using Sannel.Encoding.Manager.Web.Features.Omdb.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Omdb.Services;

/// <summary>
/// Retrieves movie data from OMDB (Open Movie Database).
/// </summary>
public interface IOmdbService
{
	/// <summary>
	/// Returns true if the OMDb service is configured and available.
	/// </summary>
	bool IsConfigured { get; }

	/// <summary>
	/// Returns movie data for the given IMDb ID (e.g. "tt1234567"), or null if not found.
	/// </summary>
	Task<OmdbMovie?> GetMovieAsync(string imdbId, CancellationToken ct = default);

	/// <summary>
	/// Returns movie data for the given title, or null if not found.
	/// Uses the first search result from OMDb.
	/// </summary>
	Task<OmdbMovie?> SearchMovieAsync(string title, CancellationToken ct = default);
}
