namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Result of <c>queue_encode_job</c>.</summary>
public class McpQueueEncodeResult
{
	public required Guid QueueItemId { get; init; }

	public required int TrackCount { get; init; }

	/// <summary>Tracks dropped because their output name was blank.</summary>
	public required int SkippedTracks { get; init; }
}
