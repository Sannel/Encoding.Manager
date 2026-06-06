using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.BackgroundServices;

public class PlayStateSyncWorker : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<PlayStateSyncWorker> _logger;

	public PlayStateSyncWorker(
		IServiceScopeFactory scopeFactory,
		ILogger<PlayStateSyncWorker> logger)
	{
		this._scopeFactory = scopeFactory;
		this._logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		this._logger.LogInformation("PlayStateSyncWorker starting.");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await this.ProcessDueProfilesAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Error in play-state sync cycle.");
			}

			await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken).ConfigureAwait(false);
		}

		this._logger.LogInformation("PlayStateSyncWorker stopping.");
	}

	private async Task ProcessDueProfilesAsync(CancellationToken ct)
	{
		await using var scope = this._scopeFactory.CreateAsyncScope();
		var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
		var syncService = scope.ServiceProvider.GetRequiredService<IJellyfinSyncService>();

		await using var ctx = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var enabledProfiles = await ctx.JellyfinSyncProfiles
			.Include(p => p.ServerA)
			.Include(p => p.ServerB)
			.Where(p => p.IsEnabled)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		var dueProfiles = enabledProfiles
			.Where(p => p.LastSyncedAt is null ||
				(now - p.LastSyncedAt.Value).TotalMinutes >= p.SyncIntervalMinutes)
			.ToList();

		foreach (var profile in dueProfiles)
		{
			try
			{
				this._logger.LogInformation("Running sync for profile {ProfileName}.", profile.Name);
				await syncService.SyncProfileAsync(profile, ct: ct).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Sync failed for profile {ProfileId}.", profile.Id);
			}
		}
	}
}
