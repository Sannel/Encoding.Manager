using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

/// <summary>
/// No-op scan cache provider for the runner.
/// The runner performs fresh scans and caches in memory per job, not in a database.
/// </summary>
public class NoOpScanCacheProvider : IScanCacheProvider
{
	public Task<string?> GetCachedScanJsonAsync(string inputPath, CancellationToken ct = default) =>
		Task.FromResult<string?>(null);

	public Task SaveScanAsync(string inputPath, string scanJson, CancellationToken ct = default) =>
		Task.CompletedTask;
}
