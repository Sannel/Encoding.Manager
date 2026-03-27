using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public class EncodeQueueService : IEncodeQueueService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public EncodeQueueService(IDbContextFactory<AppDbContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	/// <inheritdoc />
	public async Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default)
	{
		item.DiscPath = PathHelper.ToForwardSlash(item.DiscPath);
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		ctx.EncodeQueueItems.Add(item);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
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
		await ctx.EncodeQueueItems
			.Where(i => i.Id == id)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);
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
	}
}
