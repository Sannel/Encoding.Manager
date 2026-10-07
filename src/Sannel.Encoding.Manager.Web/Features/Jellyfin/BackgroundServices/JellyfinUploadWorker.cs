using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Options;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.BackgroundServices;

public class JellyfinUploadWorker : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly JellyfinOptions _options;
	private readonly ILogger<JellyfinUploadWorker> _logger;

	public JellyfinUploadWorker(
		IServiceScopeFactory scopeFactory,
		IOptions<JellyfinOptions> options,
		ILogger<JellyfinUploadWorker> logger)
	{
		this._scopeFactory = scopeFactory;
		this._options = options.Value;
		this._logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		this._logger.LogInformation("JellyfinUploadWorker starting.");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await this.ProcessPendingUploadsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Error in Jellyfin upload cycle.");
			}

			await Task.Delay(TimeSpan.FromSeconds(this._options.UploadPollIntervalSeconds), stoppingToken).ConfigureAwait(false);
		}

		this._logger.LogInformation("JellyfinUploadWorker stopping.");
	}

	private async Task ProcessPendingUploadsAsync(CancellationToken ct)
	{
		await using var scope = this._scopeFactory.CreateAsyncScope();
		var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
		var encodeService = scope.ServiceProvider.GetRequiredService<IJellyfinEncodeService>();

		await using var ctx = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var pendingItems = await ctx.EncodeQueueItems
			.Where(i => i.Status == "Finished" && i.JellyfinUploadStatus == "Pending")
			.Select(i => i.Id)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		foreach (var itemId in pendingItems)
		{
			try
			{
				await encodeService.HandleEncodeCompletedAsync(itemId, ct).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Failed to handle post-encode upload for job {JobId}.", itemId);
			}
		}
	}
}
