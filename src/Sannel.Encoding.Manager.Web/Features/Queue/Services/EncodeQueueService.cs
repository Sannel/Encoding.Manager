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
	private readonly QueueChangeNotifier _notifier;

	public EncodeQueueService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<QueueHub> hubContext, QueueChangeNotifier notifier)
	{
		_dbFactory = dbFactory;
		_hubContext = hubContext;
		_notifier = notifier;
	}

	/// <inheritdoc />
	public async Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default)
	{
		item.DiscPath = PathHelper.ToForwardSlash(item.DiscPath);
		item.IsArchived = false;
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		item.SortOrder = (await ctx.EncodeQueueItems.MaxAsync(i => (int?)i.SortOrder, ct).ConfigureAwait(false) ?? -1) + 1;
		ctx.EncodeQueueItems.Add(item);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EncodeQueueItem>> GetItemsAsync(bool includeCleared = false, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var query = ctx.EncodeQueueItems.AsQueryable();
		if (!includeCleared)
		{
			query = query.Where(i => !i.IsArchived);
		}

		return await query
			.OrderBy(i => i.SortOrder)
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
		var isCanceled = string.Equals(item.Status, "Canceled", StringComparison.OrdinalIgnoreCase);
		var isCancelRequested = string.Equals(item.Status, "CancelRequested", StringComparison.OrdinalIgnoreCase);
		if (!isFailed && !isFinished && !isCanceled && !isCancelRequested)
		{
			return false;
		}

		item.Status = "Queued";
		item.RunnerName = null;
		item.StartedAt = null;
		item.CompletedAt = null;
		item.ProgressPercent = null;
		item.CurrentTrackProgressPercent = null;
		item.IsArchived = false;
		item.SortOrder = (await ctx.EncodeQueueItems.MaxAsync(i => (int?)i.SortOrder, ct).ConfigureAwait(false) ?? -1) + 1;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> CancelEncodingAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var item = await ctx.EncodeQueueItems.FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
		if (item is null)
		{
			return false;
		}

		var isEncoding = string.Equals(item.Status, "Encoding", StringComparison.OrdinalIgnoreCase);
		if (!isEncoding)
		{
			return false;
		}

		item.Status = "CancelRequested";
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc />
	public async Task<int> ClearFinishedAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var finishedItems = await ctx.EncodeQueueItems
			.Where(i => i.Status == "Finished" && !i.IsArchived)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		if (finishedItems.Count == 0)
		{
			return 0;
		}

		foreach (var item in finishedItems)
		{
			item.IsArchived = true;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

		foreach (var item in finishedItems)
		{
			await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		}

		return finishedItems.Count;
	}

	/// <inheritdoc />
	public async Task<bool> MoveItemAsync(Guid id, MoveDirection direction, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var allItems = await ctx.EncodeQueueItems
			.Where(i => !i.IsArchived)
			.OrderBy(i => i.SortOrder)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		var item = allItems.FirstOrDefault(i => i.Id == id);
		if (item is null || !string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var currentIndex = allItems.IndexOf(item);
		EncodeQueueItem? swapTarget;

		if (direction == MoveDirection.Up)
		{
			swapTarget = allItems
				.Take(currentIndex)
				.LastOrDefault(i => string.Equals(i.Status, "Queued", StringComparison.OrdinalIgnoreCase));
		}
		else
		{
			swapTarget = allItems
				.Skip(currentIndex + 1)
				.FirstOrDefault(i => string.Equals(i.Status, "Queued", StringComparison.OrdinalIgnoreCase));
		}

		if (swapTarget is null)
		{
			return false;
		}

		(item.SortOrder, swapTarget.SortOrder) = (swapTarget.SortOrder, item.SortOrder);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(swapTarget, ct).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> MoveToIndexAsync(Guid id, int targetIndex, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var allItems = await ctx.EncodeQueueItems
			.Where(i => !i.IsArchived)
			.OrderBy(i => i.SortOrder)
			.ToListAsync(ct)
			.ConfigureAwait(false);

		var item = allItems.FirstOrDefault(i => i.Id == id);
		if (item is null || !string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		// Clamp: cannot move above the last non-Queued item
		var lastLockedIndex = -1;
		for (var i = 0; i < allItems.Count; i++)
		{
			var status = allItems[i].Status;
			if (!string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase))
			{
				lastLockedIndex = i;
			}
		}

		var clampedIndex = Math.Max(targetIndex, lastLockedIndex + 1);
		clampedIndex = Math.Min(clampedIndex, allItems.Count - 1);

		// Perform the move and re-sequence SortOrder
		allItems.Remove(item);
		allItems.Insert(clampedIndex, item);
		for (var i = 0; i < allItems.Count; i++)
		{
			allItems[i].SortOrder = i;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		foreach (var updated in allItems)
		{
			await NotifyQueueItemUpsertedAsync(updated, ct).ConfigureAwait(false);
		}

		return true;
	}

	private Task NotifyQueueItemUpsertedAsync(EncodeQueueItem item, CancellationToken ct)
	{
		_notifier.NotifyItemUpserted(item);
		return _hubContext.Clients.All.SendAsync("QueueItemUpserted", item, ct);
	}

	private Task NotifyQueueItemDeletedAsync(Guid id, CancellationToken ct)
	{
		if (id == Guid.Empty)
		{
			return Task.CompletedTask;
		}

		_notifier.NotifyItemDeleted(id);
		return _hubContext.Clients.All.SendAsync("QueueItemDeleted", id, ct);
	}
}
