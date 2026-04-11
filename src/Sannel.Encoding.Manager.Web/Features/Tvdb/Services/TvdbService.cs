using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Settings.Entities;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Options;

namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

/// <summary>
/// Implements <see cref="ITvdbService"/> using TheTVDB v4 REST API.
/// Bearer token is obtained on first use and cached for the lifetime of this instance.
/// Series names and episode lists are cached in the database for <see cref="CacheDuration"/>.
/// </summary>
public class TvdbService : ITvdbService
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

	private readonly HttpClient _httpClient;
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly TvdbOptions _options;
	private string? _token;

	public TvdbService(HttpClient httpClient, IDbContextFactory<AppDbContext> dbFactory, IOptions<TvdbOptions> options)
	{
		this._httpClient = httpClient;
		this._dbFactory = dbFactory;
		this._options = options.Value;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TvdbEpisode>> GetEpisodesAsync(
		int seriesId,
		TvdbEpisodeOrderType orderType = TvdbEpisodeOrderType.Default,
		CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var orderTypeStr = GetEpisodeTypePath(orderType);

		// Check DB cache: if any episode row for this series+orderType is still fresh, serve from DB
		var latestEntry = await ctx.TvdbEpisodeCache
			.AsNoTracking()
			.Where(e => e.SeriesId == seriesId && e.OrderType == orderTypeStr)
			.OrderByDescending(e => e.CachedAt)
			.FirstOrDefaultAsync(ct)
			.ConfigureAwait(false);

		if (latestEntry is not null && latestEntry.CachedAt > DateTimeOffset.UtcNow.Add(-CacheDuration))
		{
			return await ctx.TvdbEpisodeCache
				.Where(e => e.SeriesId == seriesId && e.OrderType == orderTypeStr)
				.OrderBy(e => e.SeasonNumber)
				.ThenBy(e => e.EpisodeNumber)
				.Select(e => new TvdbEpisode
				{
					SeasonNumber = e.SeasonNumber,
					EpisodeNumber = e.EpisodeNumber,
					Name = e.Name,
				})
				.ToListAsync(ct)
				.ConfigureAwait(false);
		}

		await this.EnsureAuthenticatedAsync(ct).ConfigureAwait(false);

		// Check if the series' original language differs from the preferred language
		var preferredLang = await this.GetPreferredLanguageAsync(ctx, ct).ConfigureAwait(false);
		var useTranslation = await this.IsNonPreferredLanguageSeriesAsync(seriesId, preferredLang, ctx, ct).ConfigureAwait(false);
		var langSuffix = useTranslation ? $"/{preferredLang}" : string.Empty;

		var episodes = new List<TvdbEpisode>();
		var page = 0;

		while (true)
		{
			var response = await this._httpClient
				.GetAsync($"series/{seriesId}/episodes/{orderTypeStr}{langSuffix}?page={page}", ct)
				.ConfigureAwait(false);

			response.EnsureSuccessStatusCode();

			using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
			var root = doc.RootElement;

			if (root.TryGetProperty("data", out var data)
				&& data.TryGetProperty("episodes", out var episodesArray))
			{
				foreach (var ep in episodesArray.EnumerateArray())
				{
					var seasonNumber = ep.TryGetProperty("seasonNumber", out var sn) ? sn.GetInt32() : 0;
					var episodeNumber = ep.TryGetProperty("number", out var en) ? en.GetInt32() : 0;
					var name = ep.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
						? n.GetString() ?? string.Empty
						: string.Empty;

					episodes.Add(new TvdbEpisode
					{
						SeasonNumber = seasonNumber,
						EpisodeNumber = episodeNumber,
						Name = name
					});
				}
			}

			// Advance to next page if available
			if (root.TryGetProperty("links", out var links)
				&& links.TryGetProperty("next", out var next)
				&& next.ValueKind != JsonValueKind.Null
				&& !string.IsNullOrEmpty(next.GetString()))
			{
				page++;
			}
			else
			{
				break;
			}
		}

		var result = episodes
			.OrderBy(e => e.SeasonNumber)
			.ThenBy(e => e.EpisodeNumber)
			.ToList();

		// Persist to DB cache: replace old rows for this series+orderType
		var now = DateTimeOffset.UtcNow;
		await ctx.TvdbEpisodeCache
			.Where(e => e.SeriesId == seriesId && e.OrderType == orderTypeStr)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);

		ctx.TvdbEpisodeCache.AddRange(result.Select(ep => new TvdbEpisodeCache
		{
			SeriesId = seriesId,
			OrderType = orderTypeStr,
			SeasonNumber = ep.SeasonNumber,
			EpisodeNumber = ep.EpisodeNumber,
			Name = ep.Name,
			CachedAt = now,
		}));
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

		return result;
	}

	private static string GetEpisodeTypePath(TvdbEpisodeOrderType orderType) => orderType switch
	{
		TvdbEpisodeOrderType.Dvd => "dvd",
		TvdbEpisodeOrderType.Bluray => "absolute",
		TvdbEpisodeOrderType.Streaming => "streaming",
		_ => "official",
	};

	/// <inheritdoc />
	public async Task<string?> GetSeriesNameAsync(int seriesId, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		// Check DB cache
		var cached = await ctx.TvdbSeriesCache
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.SeriesId == seriesId, ct)
			.ConfigureAwait(false);

		if (cached is not null && cached.CachedAt > DateTimeOffset.UtcNow.Add(-CacheDuration))
		{
			return cached.Name;
		}

		await this.EnsureAuthenticatedAsync(ct).ConfigureAwait(false);

		var response = await this._httpClient
			.GetAsync($"series/{seriesId}", ct)
			.ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

		var seriesName = doc.RootElement.TryGetProperty("data", out var data)
			&& data.TryGetProperty("name", out var name)
			&& name.ValueKind == JsonValueKind.String
				? name.GetString()
				: null;

		var originalLanguage = data.TryGetProperty("originalLanguage", out var lang)
			&& lang.ValueKind == JsonValueKind.String
				? lang.GetString()
				: null;

		// If the series is not in the preferred language, fetch the translated name
		var preferredLang = await this.GetPreferredLanguageAsync(ct).ConfigureAwait(false);
		if (!string.Equals(originalLanguage, preferredLang, StringComparison.OrdinalIgnoreCase))
		{
			var translatedName = await this.GetTranslatedSeriesNameAsync(seriesId, preferredLang, ct).ConfigureAwait(false);
			if (!string.IsNullOrEmpty(translatedName))
			{
				seriesName = translatedName;
			}
		}

		// Persist to DB cache
		await ctx.TvdbSeriesCache
			.Where(e => e.SeriesId == seriesId)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);

		ctx.TvdbSeriesCache.Add(new TvdbSeriesCache
		{
			SeriesId = seriesId,
			Name = seriesName,
			OriginalLanguage = originalLanguage,
			CachedAt = DateTimeOffset.UtcNow,
		});
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

		return seriesName;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TvdbCachedSeries>> GetCachedSeriesAsync(CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.TvdbSeriesCache
			.AsNoTracking()
			.Where(s => s.Name != null)
			.OrderBy(s => s.Name)
			.Select(s => new TvdbCachedSeries { SeriesId = s.SeriesId, Name = s.Name! })
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Fetches the translated series name from TVDB for the given language.
	/// Returns null if the translation is not available.
	/// </summary>
	private async Task<string?> GetTranslatedSeriesNameAsync(int seriesId, string language, CancellationToken ct)
	{
		try
		{
			var response = await this._httpClient
				.GetAsync($"series/{seriesId}/translations/{language}", ct)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				return null;
			}

			using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

			return doc.RootElement.TryGetProperty("data", out var data)
				&& data.TryGetProperty("name", out var name)
				&& name.ValueKind == JsonValueKind.String
					? name.GetString()
					: null;
		}
		catch (HttpRequestException)
		{
			return null;
		}
	}

	/// <summary>
	/// Checks the series cache to determine if the series' original language differs from the preferred language.
	/// If not cached, fetches and caches the series info first.
	/// </summary>
	private async Task<bool> IsNonPreferredLanguageSeriesAsync(int seriesId, string preferredLanguage, AppDbContext ctx, CancellationToken ct)
	{
		var cached = await ctx.TvdbSeriesCache
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.SeriesId == seriesId, ct)
			.ConfigureAwait(false);

		if (cached is not null && cached.OriginalLanguage is not null)
		{
			return !string.Equals(cached.OriginalLanguage, preferredLanguage, StringComparison.OrdinalIgnoreCase);
		}

		// Not cached yet — fetch the series to populate the cache (including OriginalLanguage)
		await this.GetSeriesNameAsync(seriesId, ct).ConfigureAwait(false);

		// Re-read from cache
		cached = await ctx.TvdbSeriesCache
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.SeriesId == seriesId, ct)
			.ConfigureAwait(false);

		if (cached?.OriginalLanguage is not null)
		{
			return !string.Equals(cached.OriginalLanguage, preferredLanguage, StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}

	/// <summary>
	/// Reads the preferred TVDB language from the AppSettings table.
	/// Defaults to "eng" if no setting is configured.
	/// </summary>
	private async Task<string> GetPreferredLanguageAsync(AppDbContext ctx, CancellationToken ct)
	{
		var settings = await ctx.AppSettings
			.AsNoTracking()
			.FirstOrDefaultAsync(ct)
			.ConfigureAwait(false);

		return settings?.TvdbLanguage ?? "eng";
	}

	/// <summary>
	/// Reads the preferred TVDB language from a new DbContext.
	/// Defaults to "eng" if no setting is configured.
	/// </summary>
	private async Task<string> GetPreferredLanguageAsync(CancellationToken ct)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await this.GetPreferredLanguageAsync(ctx, ct).ConfigureAwait(false);
	}

	private async Task EnsureAuthenticatedAsync(CancellationToken ct)
	{
		if (this._token is not null)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(this._options.ApiKey))
		{
			throw new InvalidOperationException(
				"TVDB API key is not configured. Set 'Tvdb:ApiKey' in user-secrets or environment variables.");
		}

		var loginPayload = new { apikey = this._options.ApiKey };
		var response = await this._httpClient
			.PostAsJsonAsync("login", loginPayload, ct)
			.ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

		this._token = doc.RootElement
			.GetProperty("data")
			.GetProperty("token")
			.GetString()
			?? throw new InvalidOperationException("TVDB login response contained an empty token.");

		this._httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", this._token);
	}
}
