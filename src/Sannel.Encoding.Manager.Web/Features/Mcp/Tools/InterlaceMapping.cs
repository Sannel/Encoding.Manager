using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>Maps interlace verdicts to the lower-case strings the MCP tools return.</summary>
internal static class InterlaceMapping
{
	public static string ToMcp(InterlaceVerdict verdict) => verdict.ToString().ToLowerInvariant();

	/// <summary>Seconds to wait before polling again while any verdict is pending.</summary>
	public const int PendingPollSeconds = 20;
}
