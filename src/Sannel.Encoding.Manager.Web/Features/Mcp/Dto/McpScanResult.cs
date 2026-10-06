namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Result of <c>scan_disc</c> / <c>get_scan_status</c>.</summary>
public class McpScanResult
{
	/// <summary>"Completed", "Scanning" or "Failed".</summary>
	public required string Status { get; init; }

	public DateTimeOffset? StartedAt { get; init; }

	/// <summary>
	/// When <see cref="Status"/> is "Scanning", or a title's interlace verdict is still "pending": wait this long, then call
	/// <c>get_scan_status</c>.
	/// </summary>
	public int? PollAfterSeconds { get; init; }

	public string? Error { get; init; }

	/// <summary>Titles at least <c>minimumDurationSeconds</c> long, when completed.</summary>
	public IReadOnlyList<McpTitleSummary>? Titles { get; init; }

	/// <summary>Number of titles hidden by the minimum duration filter.</summary>
	public int? HiddenShortTitles { get; init; }
}
