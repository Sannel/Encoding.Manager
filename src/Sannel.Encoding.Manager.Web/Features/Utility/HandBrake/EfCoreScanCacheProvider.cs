using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Scan.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>
/// EF Core implementation of <see cref="IScanCacheProvider"/>
/// backed by the <see cref="DiscScanCache"/> table.
/// </summary>
public class EfCoreScanCacheProvider : IScanCacheProvider
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public EfCoreScanCacheProvider(IDbContextFactory<AppDbContext> dbFactory) =>
		_dbFactory = dbFactory;

	public async Task<string?> GetCachedScanJsonAsync(string inputPath, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var cached = await ctx.DiscScanCache
			.FirstOrDefaultAsync(c => c.InputPath == inputPath, ct)
			.ConfigureAwait(false);

		if (cached is not null && cached.CachedAt > DateTimeOffset.UtcNow.AddHours(-24))
		{
			return cached.ScanJson;
		}

		return null;
	}

	public async Task SaveScanAsync(string inputPath, string scanJson, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		await ctx.DiscScanCache
			.Where(c => c.InputPath == inputPath)
			.ExecuteDeleteAsync(ct)
			.ConfigureAwait(false);

		ctx.DiscScanCache.Add(new DiscScanCache
		{
			InputPath = inputPath,
			ScanJson = scanJson,
			CachedAt = DateTimeOffset.UtcNow,
		});
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
	}
}
