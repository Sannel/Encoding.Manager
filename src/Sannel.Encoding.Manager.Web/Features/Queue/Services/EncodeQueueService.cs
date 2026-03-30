using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Hubs;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public class EncodeQueueService : IEncodeQueueService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IHubContext<QueueHub> _hubContext;

	public EncodeQueueService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<QueueHub> hubContext)
	{
		_dbFactory = dbFactory;
		_hubContext = hubContext;
	}

	/// <inheritdoc />
	public async Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default)
	{
		item.DiscPath = PathHelper.ToForwardSlash(item.DiscPath);
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		ctx.EncodeQueueItems.Add(item);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EncodeQueueItem>> GetItemsAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.EncodeQueueItems
			.OrderBy(i => i.CreatedAt)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DeleteItemAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var deletedCount = await ctx.EncodeQueueItems
			.Where(i => i.Id == id)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);

		if (deletedCount > 0)
		{
			await NotifyQueueItemDeletedAsync(id, ct).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task UpdateTracksAsync(Guid id, List<EncodeTrackConfig> tracks, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var item = await ctx.EncodeQueueItems.FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
		if (item is null)
		{
			return;
		}

		item.TracksJson = JsonSerializer.Serialize(tracks);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> ResetToQueuedAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var item = await ctx.EncodeQueueItems.FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
		if (item is null)
		{
			return false;
		}

		var isFailed = string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase);
		var isFinished = string.Equals(item.Status, "Finished", StringComparison.OrdinalIgnoreCase);
		if (!isFailed && !isFinished)
		{
			return false;
		}

		item.Status = "Queued";
		item.RunnerName = null;
		item.StartedAt = null;
		item.CompletedAt = null;
		item.ProgressPercent = null;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		return true;
	}

	private Task NotifyQueueItemUpsertedAsync(EncodeQueueItem item, CancellationToken ct) =>
		_hubContext.Clients.All.SendAsync("QueueItemUpserted", item, ct);

	private Task NotifyQueueItemDeletedAsync(Guid id, CancellationToken ct) =>
		id == Guid.Empty
			? Task.CompletedTask
			: _hubContext.Clients.All.SendAsync("QueueItemDeleted", id, ct);
}
