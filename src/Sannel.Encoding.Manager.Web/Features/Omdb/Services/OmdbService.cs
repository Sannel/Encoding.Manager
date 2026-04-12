using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Omdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Omdb.Options;

namespace Sannel.Encoding.Manager.Web.Features.Omdb.Services;

/// <summary>
/// Implements <see cref="IOmdbService"/> using the OMDb (Open Movie Database) REST API.
/// Movie data is not cached; fresh results are fetched on each call.
/// </summary>
public class OmdbService : IOmdbService
{
	private readonly HttpClient _httpClient;
	private readonly OmdbOptions _options;

	public OmdbService(HttpClient httpClient, IOptions<OmdbOptions> options)
	{
		this._httpClient = httpClient;
		this._options = options.Value;
	}

	/// <inheritdoc />
	public bool IsConfigured => !string.IsNullOrWhiteSpace(this._options.ApiKey);

	/// <inheritdoc />
	public async Task<OmdbMovie?> GetMovieAsync(string imdbId, CancellationToken ct = default)
	{
		if (!this.IsConfigured)
		{
			return null;
		}

		var baseUrl = this._options.BaseUrl ?? "https://www.omdbapi.com/";
		var queryString = $"?apikey={HttpUtility.UrlEncode(this._options.ApiKey)}&i={HttpUtility.UrlEncode(imdbId)}&type=movie";

		try
		{
			var response = await this._httpClient.GetAsync($"{baseUrl}{queryString}", ct).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

			return this.ParseMovieResponse(doc.RootElement);
		}
		catch
		{
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<OmdbMovie?> SearchMovieAsync(string title, CancellationToken ct = default)
	{
		if (!this.IsConfigured)
		{
			return null;
		}

		var baseUrl = this._options.BaseUrl ?? "https://www.omdbapi.com/";
		var queryString = $"?apikey={HttpUtility.UrlEncode(this._options.ApiKey)}&s={HttpUtility.UrlEncode(title)}&type=movie";

		try
		{
			var response = await this._httpClient.GetAsync($"{baseUrl}{queryString}", ct).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

			var root = doc.RootElement;

			// Check if search was successful
			if (!root.TryGetProperty("Response", out var responseElem) || responseElem.GetString() != "True")
			{
				return null;
			}

			// Get first result
			if (root.TryGetProperty("Search", out var searchArray) && searchArray.ValueKind == JsonValueKind.Array)
			{
				var firstResult = searchArray.EnumerateArray().FirstOrDefault();
				if (firstResult.ValueKind != JsonValueKind.Undefined)
				{
					// Get the IMDB ID from the search result and fetch full details
					if (firstResult.TryGetProperty("imdbID", out var imdbId) && imdbId.ValueKind == JsonValueKind.String)
					{
						var imdbIdStr = imdbId.GetString();
						if (!string.IsNullOrEmpty(imdbIdStr))
						{
							return await this.GetMovieAsync(imdbIdStr, ct).ConfigureAwait(false);
						}
					}
				}
			}

			return null;
		}
		catch
		{
			return null;
		}
	}

	private OmdbMovie? ParseMovieResponse(JsonElement root)
	{
		if (!root.TryGetProperty("Response", out var responseElem) || responseElem.GetString() != "True")
		{
			return null;
		}

		var imdbId = root.TryGetProperty("imdbID", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() ?? string.Empty : string.Empty;
		var title = root.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? string.Empty : string.Empty;
		var year = root.TryGetProperty("Year", out var y) && y.ValueKind == JsonValueKind.String ? y.GetString() ?? string.Empty : string.Empty;
		var plot = root.TryGetProperty("Plot", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : string.Empty;
		var genres = root.TryGetProperty("Genre", out var g) && g.ValueKind == JsonValueKind.String ? g.GetString() ?? string.Empty : string.Empty;

		if (string.IsNullOrEmpty(imdbId) || string.IsNullOrEmpty(title))
		{
			return null;
		}

		return new OmdbMovie
		{
			ImdbId = imdbId,
			Title = title,
			Year = year,
			Plot = plot,
			Genres = genres,
		};
	}
}
