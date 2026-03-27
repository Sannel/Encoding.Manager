namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Abstraction over disc scan result caching, allowing different
/// implementations (EF Core, in-memory, no-op, etc.).
/// </summary>
public interface IScanCacheProvider
{
	/// <summary>
	/// Retrieves the cached scan JSON for <paramref name="inputPath"/>
	/// if it was cached within the last 24 hours. Returns null otherwise.
	/// </summary>
	Task<string?> GetCachedScanJsonAsync(string inputPath, CancellationToken ct = default);

	/// <summary>
	/// Stores the raw scan JSON for <paramref name="inputPath"/>
	/// in the cache, replacing any previous entry.
	/// </summary>
	Task SaveScanAsync(string inputPath, string scanJson, CancellationToken ct = default);
}
