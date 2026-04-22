using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinSyncService : IJellyfinSyncService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IJellyfinClientFactory _clientFactory;
	private readonly JellyfinServerService _serverService;
	private readonly ILogger<JellyfinSyncService> _logger;

	public JellyfinSyncService(
		IDbContextFactory<AppDbContext> dbFactory,
		IJellyfinClientFactory clientFactory,
		JellyfinServerService serverService,
		ILogger<JellyfinSyncService> logger)
	{
		this._dbFactory = dbFactory;
		this._clientFactory = clientFactory;
		this._serverService = serverService;
		this._logger = logger;
	}

	public async Task<IReadOnlyList<JellyfinSyncProfile>> GetAllSyncProfilesAsync(CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinSyncProfiles
			.AsNoTracking()
			.Include(p => p.ServerA)
			.Include(p => p.ServerB)
			.OrderBy(p => p.Name)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	public async Task<JellyfinSyncProfile?> GetSyncProfileAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinSyncProfiles
			.AsNoTracking()
			.Include(p => p.ServerA)
			.Include(p => p.ServerB)
			.FirstOrDefaultAsync(p => p.Id == id, ct)
			.ConfigureAwait(false);
	}

	public async Task<JellyfinSyncProfile> CreateSyncProfileAsync(JellyfinSyncProfileDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var profile = new JellyfinSyncProfile
		{
			Name = dto.Name,
			ServerAId = dto.ServerAId,
			UserIdA = dto.UserIdA,
			ServerBId = dto.ServerBId,
			UserIdB = dto.UserIdB,
			IsEnabled = dto.IsEnabled,
			SyncIntervalMinutes = dto.SyncIntervalMinutes,
		};
		ctx.JellyfinSyncProfiles.Add(profile);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return profile;
	}

	public async Task<JellyfinSyncProfile?> UpdateSyncProfileAsync(Guid id, JellyfinSyncProfileDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var profile = await ctx.JellyfinSyncProfiles.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
		if (profile is null)
		{
			return null;
		}

		profile.Name = dto.Name;
		profile.ServerAId = dto.ServerAId;
		profile.UserIdA = dto.UserIdA;
		profile.ServerBId = dto.ServerBId;
		profile.UserIdB = dto.UserIdB;
		profile.IsEnabled = dto.IsEnabled;
		profile.SyncIntervalMinutes = dto.SyncIntervalMinutes;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return profile;
	}

	public async Task<bool> DeleteSyncProfileAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var profile = await ctx.JellyfinSyncProfiles.FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
		if (profile is null)
		{
			return false;
		}

		ctx.JellyfinSyncProfiles.Remove(profile);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return true;
	}

	public async Task SyncProfileAsync(JellyfinSyncProfile profile, IProgress<(int Processed, int Total)>? progress = null, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var tracked = await ctx.JellyfinSyncProfiles
			.Include(p => p.ServerA)
			.Include(p => p.ServerB)
			.FirstOrDefaultAsync(p => p.Id == profile.Id, ct)
			.ConfigureAwait(false);

		if (tracked?.ServerA is null || tracked.ServerB is null)
		{
			this._logger.LogWarning("Sync profile {ProfileId} has missing server references.", profile.Id);
			return;
		}

		try
		{
			var clientA = this._clientFactory.CreateClient(
				tracked.ServerA.BaseUrl,
				this._serverService.DecryptApiKey(tracked.ServerA.ApiKey));

			var clientB = this._clientFactory.CreateClient(
				tracked.ServerB.BaseUrl,
				this._serverService.DecryptApiKey(tracked.ServerB.ApiKey));

			await this.SyncPlayStatesAsync(clientA, tracked.UserIdA, clientB, tracked.UserIdB, progress, ct).ConfigureAwait(false);

			tracked.LastSyncedAt = DateTimeOffset.UtcNow;
			tracked.LastSyncStatus = "Success";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

			this._logger.LogInformation("Sync completed for profile {ProfileName}.", tracked.Name);
		}
		catch (Exception ex)
		{
			this._logger.LogError(ex, "Sync failed for profile {ProfileId}.", profile.Id);
			tracked.LastSyncedAt = DateTimeOffset.UtcNow;
			tracked.LastSyncStatus = $"Failed: {ex.Message}";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		}
	}

	private async Task SyncPlayStatesAsync(
		IJellyfinClient clientA, string userIdA,
		IJellyfinClient clientB, string userIdB,
		IProgress<(int Processed, int Total)>? progress,
		CancellationToken ct)
	{
		var itemsA = await this.FetchAllUserItemsAsync(clientA, userIdA, ct).ConfigureAwait(false);
		var itemsB = await this.FetchAllUserItemsAsync(clientB, userIdB, ct).ConfigureAwait(false);

		var lookupA = BuildItemLookup(itemsA);
		var lookupB = BuildItemLookup(itemsB);

		// Only process items that exist on both servers — true A↔B bidirectional sync
		var keys = lookupA.Keys.Intersect(lookupB.Keys, StringComparer.OrdinalIgnoreCase).ToList();
		var total = keys.Count;
		var processed = 0;
		var synced = 0;
		progress?.Report((0, total));

		foreach (var key in keys)
		{
			var itemA = lookupA[key];
			var itemB = lookupB[key];

			var dataA = itemA.UserData;
			var dataB = itemB.UserData;

			var dateA = dataA?.LastPlayedDate;
			var dateB = dataB?.LastPlayedDate;

			if (dateA is null && dateB is null)
			{
				progress?.Report((++processed, total));
				continue;
			}

			var aWins = dateA.HasValue && (!dateB.HasValue || dateA > dateB);
			if (aWins)
			{
				// A's activity is more recent — push A's state to B
				if (dataA!.Played)
				{
					if (dataB is not { Played: true })
					{
						await clientB.MarkPlayedAsync(userIdB, itemB.Id, ct).ConfigureAwait(false);
						synced++;
					}
				}
				else
				{
					await clientB.UpdatePlaybackPositionAsync(userIdB, itemB.Id, dataA.PlaybackPositionTicks, ct).ConfigureAwait(false);
					synced++;
				}
			}
			else
			{
				// B's activity is more recent — push B's state to A
				if (dataB!.Played)
				{
					if (dataA is not { Played: true })
					{
						await clientA.MarkPlayedAsync(userIdA, itemA.Id, ct).ConfigureAwait(false);
						synced++;
					}
				}
				else
				{
					await clientA.UpdatePlaybackPositionAsync(userIdA, itemA.Id, dataB.PlaybackPositionTicks, ct).ConfigureAwait(false);
					synced++;
				}
			}

			progress?.Report((++processed, total));
		}

		this._logger.LogInformation("Synced {Count} play state/position updates.", synced);
	}

	public async Task<IReadOnlyList<SyncCompareRow>> GetComparisonAsync(JellyfinSyncProfile profile, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var tracked = await ctx.JellyfinSyncProfiles
			.Include(p => p.ServerA)
			.Include(p => p.ServerB)
			.FirstOrDefaultAsync(p => p.Id == profile.Id, ct)
			.ConfigureAwait(false);

		if (tracked?.ServerA is null || tracked.ServerB is null)
		{
			return [];
		}

		var clientA = this._clientFactory.CreateClient(
			tracked.ServerA.BaseUrl,
			this._serverService.DecryptApiKey(tracked.ServerA.ApiKey));
		var clientB = this._clientFactory.CreateClient(
			tracked.ServerB.BaseUrl,
			this._serverService.DecryptApiKey(tracked.ServerB.ApiKey));

		var itemsA = await this.FetchAllUserItemsAsync(clientA, tracked.UserIdA, ct).ConfigureAwait(false);
		var itemsB = await this.FetchAllUserItemsAsync(clientB, tracked.UserIdB, ct).ConfigureAwait(false);

		var lookupA = BuildItemLookup(itemsA);
		var lookupB = BuildItemLookup(itemsB);

		var allKeys = lookupA.Keys.Union(lookupB.Keys, StringComparer.OrdinalIgnoreCase).ToList();

		return allKeys
			.Select(key =>
			{
				var itemA = lookupA.GetValueOrDefault(key);
				var itemB = lookupB.GetValueOrDefault(key);
				var rep = itemA ?? itemB!;
				return new SyncCompareRow(GetDisplayName(rep), rep.Type, itemA?.UserData, itemB?.UserData);
			})
			.OrderBy(r => r.DisplayName)
			.ToList();
	}

	private static string GetDisplayName(JellyfinItem item)
	{
		if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
		{
			var year = item.ProductionYear.HasValue ? $" ({item.ProductionYear})" : "";
			var se = $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2}";
			return $"{item.SeriesName}{year} {se} - {item.Name}";
		}

		return item.ProductionYear.HasValue
			? $"{item.Name} ({item.ProductionYear})"
			: item.Name;
	}

	private async Task<List<JellyfinItem>> FetchAllUserItemsAsync(IJellyfinClient client, string userId, CancellationToken ct)
	{
		var allItems = new List<JellyfinItem>();
		var startIndex = 0;
		const int pageSize = 500;

		while (true)
		{
			var response = await client.GetUserItemsAsync(userId, new GetItemsRequest
			{
				IncludeItemTypes = "Movie,Episode",
				Recursive = true,
				StartIndex = startIndex,
				Limit = pageSize,
				Fields = "UserData",
			}, ct).ConfigureAwait(false);

			allItems.AddRange(response.Items);

			if (allItems.Count >= response.TotalRecordCount || response.Items.Length == 0)
			{
				break;
			}

			startIndex += pageSize;
		}

		return allItems;
	}

	private static Dictionary<string, JellyfinItem> BuildItemLookup(List<JellyfinItem> items)
	{
		var lookup = new Dictionary<string, JellyfinItem>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in items)
		{
			var key = GetMatchKey(item);
			if (key is not null)
			{
				lookup.TryAdd(key, item);
			}
		}

		return lookup;
	}

	private static string? GetMatchKey(JellyfinItem item)
	{
		if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
		{
			var series = item.SeriesName?.Trim();
			var season = item.ParentIndexNumber;
			var episode = item.IndexNumber;
			if (string.IsNullOrEmpty(series) || season is null || episode is null)
			{
				return null;
			}

			return item.ProductionYear.HasValue
				? $"episode:{series}|{item.ProductionYear}|s{season:D2}e{episode:D2}"
				: $"episode:{series}|s{season:D2}e{episode:D2}";
		}

		if (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase))
		{
			var name = item.Name?.Trim();
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}

			return item.ProductionYear.HasValue
				? $"movie:{name}|{item.ProductionYear}"
				: $"movie:{name}";
		}

		return null;
	}
}
