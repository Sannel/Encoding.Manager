using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class ForcedRescanLimiterTests
{
	[Fact]
	public void TryAcquire_StopsAfterLimit_PerCaller()
	{
		using var limiter = new ForcedRescanLimiter(Options.Create(new McpOptions { ForcedRescanPermitLimit = 3, ForcedRescanWindowMinutes = 10 }));

		Assert.True(limiter.TryAcquire("a", out _));
		Assert.True(limiter.TryAcquire("a", out _));
		Assert.True(limiter.TryAcquire("a", out _));
		Assert.False(limiter.TryAcquire("a", out var retryAfter));
		Assert.NotNull(retryAfter);
		Assert.True(limiter.TryAcquire("b", out _));
	}
}
