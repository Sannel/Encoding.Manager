using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinEncodeService : IJellyfinEncodeService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IJellyfinClientFactory _clientFactory;
	private readonly JellyfinServerService _serverService;
	private readonly IJellyfinPathBuilder _pathBuilder;
	private readonly IJellyfinSftpService _sftpService;
	private readonly ILogger<JellyfinEncodeService> _logger;

	public JellyfinEncodeService(
		IDbContextFactory<AppDbContext> dbFactory,
		IJellyfinClientFactory clientFactory,
		JellyfinServerService serverService,
		IJellyfinPathBuilder pathBuilder,
		IJellyfinSftpService sftpService,
		ILogger<JellyfinEncodeService> logger)
	{
		this._dbFactory = dbFactory;
		this._clientFactory = clientFactory;
		this._serverService = serverService;
		this._pathBuilder = pathBuilder;
		this._sftpService = sftpService;
		this._logger = logger;
	}

	public async Task<EncodeQueueItem> QueueItemAsync(JellyfinEncodeRequest request, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var sourceServer = await ctx.JellyfinServers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == request.ServerId, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException("Source server not found.");

		var destRoot = await ctx.JellyfinDestinationRoots
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Id == request.DestRootId, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException("Destination root not found.");

		// Fetch item metadata from Jellyfin
		var client = this._clientFactory.CreateClient(sourceServer.BaseUrl, this._serverService.DecryptApiKey(sourceServer.ApiKey));
		var users = await client.GetUsersAsync(ct).ConfigureAwait(false);
		var userId = users.FirstOrDefault()?.Id;
		var item = await client.GetItemAsync(request.ItemId, userId, ct).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Item '{request.ItemId}' not found on source server.");

		// Build the remote SFTP path
		var relativePath = this._pathBuilder.BuildRemotePath(destRoot, item);

		// Build a single track config for the Jellyfin source file (always title 1)
		var outputName = Path.GetFileNameWithoutExtension(relativePath);
		var isEpisode = string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase);

		var track = new EncodeTrackConfig
		{
			TitleNumber = 1,
			OutputName = outputName,
			SeasonNumber = isEpisode ? item.ParentIndexNumber : null,
			EpisodeNumber = isEpisode ? item.IndexNumber : null,
			MovieYear = !isEpisode ? item.ProductionYear?.ToString() : null,
			PresetLabel = string.IsNullOrWhiteSpace(request.PresetLabel) ? null : request.PresetLabel,
		};
		var tracksJson = JsonSerializer.Serialize(new[] { track });

		// Determine the max sort order
		var maxSortOrder = await ctx.EncodeQueueItems
			.Where(i => i.Status == "Queued")
			.MaxAsync(i => (int?)i.SortOrder, ct)
			.ConfigureAwait(false) ?? 0;

		// Parse TVDB ID from provider IDs
		var tvdbString = isEpisode ? item.SeriesProviderIds?.Tvdb : item.ProviderIds?.Tvdb;
		int? tvdbId = int.TryParse(tvdbString, out var parsed) ? parsed : null;

		var queueItem = new EncodeQueueItem
		{
			DiscPath = string.Empty,
			Mode = "Titles",
			Status = "Queued",
			TracksJson = tracksJson,
			TvdbShowName = isEpisode ? item.SeriesName : item.Name,
			TvdbId = tvdbId,
			SortOrder = maxSortOrder + 1,
			JellyfinSourceServerId = sourceServer.Id,
			JellyfinSourceItemId = request.ItemId,
			JellyfinDestServerId = request.DestServerId,
			JellyfinDestRootId = request.DestRootId,
			JellyfinDestRelativePath = relativePath,
			JellyfinUploadStatus = "Pending",
		};

		ctx.EncodeQueueItems.Add(queueItem);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

		this._logger.LogInformation("Queued Jellyfin item {ItemId} from server {ServerId} as job {JobId}.",
			request.ItemId, request.ServerId, queueItem.Id);

		return queueItem;
	}

	public async Task HandleEncodeCompletedAsync(Guid queueItemId, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var item = await ctx.EncodeQueueItems
			.FirstOrDefaultAsync(i => i.Id == queueItemId, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException($"Queue item {queueItemId} not found.");

		if (item.JellyfinDestServerId is null || item.JellyfinDestRootId is null)
		{
			return;
		}

		var destServer = await ctx.JellyfinServers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == item.JellyfinDestServerId, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException("Destination server not found.");

		var destRoot = await ctx.JellyfinDestinationRoots
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Id == item.JellyfinDestRootId, ct)
			.ConfigureAwait(false)
			?? throw new InvalidOperationException("Destination root not found.");

		var remotePath = $"{destRoot.RootPath.TrimEnd('/')}/{item.JellyfinDestRelativePath}";

		try
		{
			item.JellyfinUploadStatus = "Uploading";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

			// Upload is only possible if we have a local file (non-Jellyfin jobs or after runner uploads).
			// For Jellyfin-sourced jobs, the runner will handle the upload — server only does library refresh.
			if (!string.IsNullOrEmpty(item.DiscPath))
			{
				await this._sftpService.UploadFileAsync(destServer, item.DiscPath, remotePath, ct).ConfigureAwait(false);
			}

			// Trigger library refresh
			var client = this._clientFactory.CreateClient(destServer.BaseUrl, this._serverService.DecryptApiKey(destServer.ApiKey));
			await client.RefreshLibraryAsync(ct).ConfigureAwait(false);

			item.JellyfinUploadStatus = "Uploaded";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

			this._logger.LogInformation("SFTP upload and library refresh complete for job {JobId}.", queueItemId);
		}
		catch (Exception ex)
		{
			this._logger.LogError(ex, "SFTP upload failed for job {JobId}. LocalPath={LocalPath}, RemotePath={RemotePath}.", queueItemId, item.DiscPath, remotePath);
			item.JellyfinUploadStatus = "Failed";
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		}
	}
}
