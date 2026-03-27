using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Runners.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Runners.Services;

/// <inheritdoc />
public class RunnerManagementService : IRunnerManagementService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public RunnerManagementService(IDbContextFactory<AppDbContext> dbFactory) =>
		_dbFactory = dbFactory;

	/// <inheritdoc />
	public async Task<IReadOnlyList<RunnerDto>> GetRunnersAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.Runners
			.AsNoTracking()
			.OrderBy(r => r.Name)
			.Select(r => new RunnerDto
			{
				Id = r.Id,
				Name = r.Name,
				IsEnabled = r.IsEnabled,
				LastSeenAt = r.LastSeenAt,
				CurrentJobId = r.CurrentJobId,
			})
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var runner = await ctx.Runners.FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
		if (runner is not null)
		{
			runner.IsEnabled = enabled;
			await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task DeleteRunnerAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		await ctx.Runners
			.Where(r => r.Id == id)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);
	}
}
