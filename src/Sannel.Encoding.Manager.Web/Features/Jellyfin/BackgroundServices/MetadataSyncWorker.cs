using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.BackgroundServices;

public class MetadataSyncWorker : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<MetadataSyncWorker> _logger;

	// Track per-pair run date so we don't re-run the same pair twice in one day.
	private readonly Dictionary<Guid, DateOnly> _lastRunDate = [];

	public MetadataSyncWorker(
		IServiceScopeFactory scopeFactory,
		ILogger<MetadataSyncWorker> logger)
	{
		this._scopeFactory = scopeFactory;
		this._logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		this._logger.LogInformation("MetadataSyncWorker starting.");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var localNow = DateTime.Now;
				// Fire only between 04:00 and 04:01 local time.
				if (localNow.Hour == 4 && localNow.Minute == 0)
				{
					await this.ProcessDuePairsAsync(stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Error in metadata sync cycle.");
			}

			await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
		}

		this._logger.LogInformation("MetadataSyncWorker stopping.");
	}

	private async Task ProcessDuePairsAsync(CancellationToken ct)
	{
		await using var scope = this._scopeFactory.CreateAsyncScope();
		var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
		var syncService = scope.ServiceProvider.GetRequiredService<IJellyfinMetadataSyncService>();

		await using var ctx = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var enabledPairs = await ctx.JellyfinMetadataServerPairs
			.Include(p => p.SourceServer)
			.Include(p => p.DestinationServer)
			.Where(p => p.IsEnabled)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		var today = DateOnly.FromDateTime(DateTime.Today);

		foreach (var pair in enabledPairs)
		{
			// Skip if already ran today for this pair.
			if (this._lastRunDate.TryGetValue(pair.Id, out var lastRun) && lastRun == today)
			{
				continue;
			}

			try
			{
				this._logger.LogInformation(
					"Running nightly metadata sync for pair {SourceServer} → {DestServer}.",
					pair.SourceServer?.Name, pair.DestinationServer?.Name);
				await syncService.SyncPairAsync(pair, ct: ct).ConfigureAwait(false);
				this._lastRunDate[pair.Id] = today;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex,
					"Metadata sync failed for pair {PairId}.", pair.Id);
			}
		}
	}
}
