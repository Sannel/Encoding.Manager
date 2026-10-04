using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <inheritdoc />
public sealed class ForcedRescanLimiter : IForcedRescanLimiter, IDisposable
{
	private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new(StringComparer.Ordinal);
	private readonly McpOptions _options;

	public ForcedRescanLimiter(IOptions<McpOptions> options) =>
		this._options = options.Value;

	/// <inheritdoc />
	public bool TryAcquire(string callerId, out TimeSpan? retryAfter)
	{
		var limiter = this._limiters.GetOrAdd(callerId, _ => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
		{
			PermitLimit = Math.Max(1, this._options.ForcedRescanPermitLimit),
			Window = TimeSpan.FromMinutes(Math.Max(1, this._options.ForcedRescanWindowMinutes)),
			SegmentsPerWindow = Math.Max(1, this._options.ForcedRescanWindowMinutes),
			QueueLimit = 0,
			AutoReplenishment = true,
		}));

		using var lease = limiter.AttemptAcquire();
		if (lease.IsAcquired)
		{
			retryAfter = null;
			return true;
		}

		retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
			? after
			: TimeSpan.FromMinutes(1);
		return false;
	}

	public void Dispose()
	{
		foreach (var limiter in this._limiters.Values)
		{
			limiter.Dispose();
		}
	}
}
