using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinMetadataSyncService : IJellyfinMetadataSyncService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IJellyfinClientFactory _clientFactory;
	private readonly JellyfinServerService _serverService;
	private readonly ILogger<JellyfinMetadataSyncService> _logger;

	public JellyfinMetadataSyncService(
		IDbContextFactory<AppDbContext> dbFactory,
		IJellyfinClientFactory clientFactory,
		JellyfinServerService serverService,
		ILogger<JellyfinMetadataSyncService> logger)
	{
		this._dbFactory = dbFactory;
		this._clientFactory = clientFactory;
		this._serverService = serverService;
		this._logger = logger;
	}

	public async Task<IReadOnlyList<JellyfinMetadataServerPair>> GetAllPairsAsync(CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinMetadataServerPairs
			.AsNoTracking()
			.Include(p => p.SourceServer)
			.Include(p => p.DestinationServer)
			.OrderBy(p => p.CreatedAt)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	public async Task<JellyfinMetadataServerPair?> GetPairAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinMetadataServerPairs
			.AsNoTracking()
			.Include(p => p.SourceServer)
			.Include(p => p.DestinationServer)
			.FirstOrDefaultAsync(p => p.Id == id, ct)
			.ConfigureAwait(false);
	}

	public async Task<JellyfinMetadataServerPair> CreatePairAsync(JellyfinMetadataServerPairDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var pair = new JellyfinMetadataServerPair
		{
			SourceServerId = dto.SourceServerId,
			DestinationServerId = dto.DestinationServerId,
			IsEnabled = dto.IsEnabled,
		};
		ctx.JellyfinMetadataServerPairs.Add(pair);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return pair;
	}

	public async Task<JellyfinMetadataServerPair?> UpdatePairAsync(Guid id, JellyfinMetadataServerPairDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var pair = await ctx.JellyfinMetadataServerPairs
			.FirstOrDefaultAsync(p => p.Id == id, ct)
			.ConfigureAwait(false);
		if (pair is null)
		{
			return null;
		}

		pair.SourceServerId = dto.SourceServerId;
		pair.DestinationServerId = dto.DestinationServerId;
		pair.IsEnabled = dto.IsEnabled;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return pair;
	}

	public async Task<bool> DeletePairAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var pair = await ctx.JellyfinMetadataServerPairs
			.FirstOrDefaultAsync(p => p.Id == id, ct)
			.ConfigureAwait(false);
		if (pair is null)
		{
			return false;
		}

		ctx.JellyfinMetadataServerPairs.Remove(pair);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return true;
	}

	public async Task SyncPairAsync(JellyfinMetadataServerPair pair, IProgress<(int Processed, int Total)>? progress = null, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var tracked = await ctx.JellyfinMetadataServerPairs
			.Include(p => p.SourceServer)
			.Include(p => p.DestinationServer)
			.FirstOrDefaultAsync(p => p.Id == pair.Id, ct)
			.ConfigureAwait(false);

		if (tracked?.SourceServer is null || tracked.DestinationServer is null)
		{
			this._logger.LogWarning("Metadata sync pair {PairId} has missing server references.", pair.Id);
			return;
		}

		try
		{
			var sourceClient = this._clientFactory.CreateClient(
				tracked.SourceServer.BaseUrl,
				this._serverService.DecryptApiKey(tracked.SourceServer.ApiKey));
			var destClient = this._clientFactory.CreateClient(
				tracked.DestinationServer.BaseUrl,
				this._serverService.DecryptApiKey(tracked.DestinationServer.ApiKey));

			await this.SyncSeriesMetadataAsync(
				sourceClient, destClient,
				tracked.SourceServer.Name, tracked.DestinationServer.Name,
				progress, ct).ConfigureAwait(false);

			tracked.LastSyncedAt = DateTimeOffset.UtcNow;
			tracked.LastSyncStatus = "Success";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

			this._logger.LogInformation(
				"Metadata sync completed for pair {SourceServer} → {DestServer}.",
				tracked.SourceServer.Name, tracked.DestinationServer.Name);
		}
		catch (Exception ex)
		{
			this._logger.LogError(ex,
				"Metadata sync failed for pair {PairId} ({SourceServer} → {DestServer}).",
				pair.Id, tracked.SourceServer.Name, tracked.DestinationServer.Name);
			tracked.LastSyncedAt = DateTimeOffset.UtcNow;
			tracked.LastSyncStatus = $"Failed: {ex.Message}";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		}
	}

	private async Task SyncSeriesMetadataAsync(
		IJellyfinClient sourceClient,
		IJellyfinClient destClient,
		string sourceName,
		string destName,
		IProgress<(int Processed, int Total)>? progress,
		CancellationToken ct)
	{
		const string seriesFields = "ProviderIds,Tags,DisplayOrder,Overview,Genres,OfficialRating,Status,Studios,PremiereDate,EndDate";

		var sourceSeries = await this.FetchAllSeriesAsync(sourceClient, seriesFields, ct).ConfigureAwait(false);
		var destSeries = await this.FetchAllSeriesAsync(destClient, seriesFields, ct).ConfigureAwait(false);

		// Build destination lookups keyed by each external ID type for cascade matching.
		var destByTvdb = BuildLookup(destSeries, s => s.ProviderIds?.Tvdb);
		var destByTvdbSlug = BuildLookup(destSeries, s => s.ProviderIds?.TvdbSlug);
		var destByImdb = BuildLookup(destSeries, s => s.ProviderIds?.Imdb);

		var total = sourceSeries.Count;
		var processed = 0;
		var updated = 0;
		progress?.Report((0, total));

		foreach (var src in sourceSeries)
		{
			var destItem = ResolveDestinationMatch(src, destByTvdb, destByTvdbSlug, destByImdb);
			if (destItem is null)
			{
				this._logger.LogDebug(
					"No match found on {DestServer} for series '{SeriesName}' (Tvdb={Tvdb}, TvdbSlug={TvdbSlug}, Imdb={Imdb}). Skipping.",
					destName, src.Name,
					src.ProviderIds?.Tvdb, src.ProviderIds?.TvdbSlug, src.ProviderIds?.Imdb);
				progress?.Report((++processed, total));
				continue;
			}

			var needsUpdate = false;

			// Determine merged DisplayOrder.
			var newDisplayOrder = destItem.DisplayOrder;
			if (!string.IsNullOrEmpty(src.DisplayOrder) &&
				!string.Equals(src.DisplayOrder, destItem.DisplayOrder, StringComparison.OrdinalIgnoreCase))
			{
				newDisplayOrder = src.DisplayOrder;
				needsUpdate = true;
			}

			// Compute additive tag merge.
			var destTags = destItem.Tags ?? [];
			var srcTags = src.Tags ?? [];
			var missingTags = srcTags
				.Where(t => !destTags.Contains(t, StringComparer.OrdinalIgnoreCase))
				.ToArray();
			var mergedTags = destTags;
			if (missingTags.Length > 0)
			{
				mergedTags = [.. destTags, .. missingTags];
				needsUpdate = true;
			}

			if (!needsUpdate)
			{
				progress?.Report((++processed, total));
				continue;
			}

			var updateRequest = new JellyfinSeriesUpdateRequest
			{
				Id = destItem.Id,
				Name = destItem.Name,
				Type = destItem.Type,
				ProductionYear = destItem.ProductionYear,
				ProviderIds = destItem.ProviderIds,
				Overview = destItem.Overview,
				Genres = destItem.Genres,
				OfficialRating = destItem.OfficialRating,
				Status = destItem.Status,
				Studios = destItem.Studios,
				PremiereDate = destItem.PremiereDate,
				EndDate = destItem.EndDate,
				Tags = mergedTags,
				DisplayOrder = newDisplayOrder,
			};

			try
			{
				await destClient.UpdateItemAsync(destItem.Id, updateRequest, ct).ConfigureAwait(false);
				updated++;
				this._logger.LogInformation(
					"Updated metadata for '{SeriesName}' on {DestServer}: DisplayOrder={DisplayOrder}, AddedTags={AddedTagCount}.",
					src.Name, destName, newDisplayOrder, missingTags.Length);
			}
			catch (Exception ex)
			{
				this._logger.LogWarning(ex,
					"Failed to update metadata for '{SeriesName}' (id={DestItemId}) on {DestServer}.",
					src.Name, destItem.Id, destName);
			}

			progress?.Report((++processed, total));
		}

		this._logger.LogInformation(
			"Metadata sync {SourceServer} → {DestServer}: {Updated} series updated out of {Total} source series.",
			sourceName, destName, updated, total);
	}

	private static JellyfinItem? ResolveDestinationMatch(
		JellyfinItem src,
		Dictionary<string, JellyfinItem> byTvdb,
		Dictionary<string, JellyfinItem> byTvdbSlug,
		Dictionary<string, JellyfinItem> byImdb)
	{
		if (!string.IsNullOrEmpty(src.ProviderIds?.Tvdb) &&
			byTvdb.TryGetValue(src.ProviderIds.Tvdb, out var matchByTvdb))
		{
			return matchByTvdb;
		}

		if (!string.IsNullOrEmpty(src.ProviderIds?.TvdbSlug) &&
			byTvdbSlug.TryGetValue(src.ProviderIds.TvdbSlug, out var matchBySlug))
		{
			return matchBySlug;
		}

		if (!string.IsNullOrEmpty(src.ProviderIds?.Imdb) &&
			byImdb.TryGetValue(src.ProviderIds.Imdb, out var matchByImdb))
		{
			return matchByImdb;
		}

		return null;
	}

	private async Task<List<JellyfinItem>> FetchAllSeriesAsync(IJellyfinClient client, string fields, CancellationToken ct)
	{
		var all = new List<JellyfinItem>();
		var startIndex = 0;
		const int pageSize = 500;

		while (true)
		{
			var response = await client.GetItemsAsync(new GetItemsRequest
			{
				IncludeItemTypes = "Series",
				Recursive = true,
				StartIndex = startIndex,
				Limit = pageSize,
				Fields = fields,
			}, ct).ConfigureAwait(false);

			all.AddRange(response.Items);

			if (all.Count >= response.TotalRecordCount || response.Items.Length == 0)
			{
				break;
			}

			startIndex += pageSize;
		}

		return all;
	}

	private static Dictionary<string, JellyfinItem> BuildLookup(
		List<JellyfinItem> items,
		Func<JellyfinItem, string?> keySelector)
	{
		var lookup = new Dictionary<string, JellyfinItem>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in items)
		{
			var key = keySelector(item);
			if (!string.IsNullOrEmpty(key))
			{
				lookup.TryAdd(key, item);
			}
		}

		return lookup;
	}
}
