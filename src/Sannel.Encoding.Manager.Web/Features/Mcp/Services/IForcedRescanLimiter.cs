namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <summary>Limits how often each API key may force a HandBrake rescan.</summary>
public interface IForcedRescanLimiter
{
	/// <summary>
	/// Takes one permit for the caller. Returns false, with a hint of when to retry, when the caller is over the limit.
	/// </summary>
	bool TryAcquire(string callerId, out TimeSpan? retryAfter);
}
