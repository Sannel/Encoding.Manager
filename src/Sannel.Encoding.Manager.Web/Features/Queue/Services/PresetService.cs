using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public class PresetService : IPresetService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public PresetService(IDbContextFactory<AppDbContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EncodingPreset>> GetPresetsAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.EncodingPresets
			.AsNoTracking()
			.OrderBy(p => p.Label)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task AddPresetAsync(EncodingPreset preset, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		ctx.EncodingPresets.Add(preset);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DeletePresetAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		await ctx.EncodingPresets
			.Where(p => p.Id == id)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);
	}
}
