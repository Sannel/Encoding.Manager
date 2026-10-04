namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A queue item returned by <c>get_queue</c>.</summary>
public class McpQueueItemSummary
{
	public required Guid Id { get; init; }

	public string? RootLabel { get; init; }

	public required string DiscPath { get; init; }

	public required string Mode { get; init; }

	public required string Status { get; init; }

	public int? ProgressPercent { get; init; }

	public required int TrackCount { get; init; }

	public string? TvdbShowName { get; init; }

	public required DateTimeOffset CreatedAt { get; init; }

	public string? CreatedBy { get; init; }

	public string? CreatedVia { get; init; }
}
