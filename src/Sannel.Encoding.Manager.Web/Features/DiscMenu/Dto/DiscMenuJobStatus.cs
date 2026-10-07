using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;

/// <summary>Snapshot of a disc menu inspection.</summary>
public class DiscMenuJobStatus
{
	public required DiscMenuJobState State { get; init; }

	public DateTimeOffset? StartedAt { get; init; }

	public DiscMenuMap? Map { get; init; }

	/// <summary>Absolute folder holding the menu screenshots when completed.</summary>
	public string? ScreenshotFolder { get; init; }

	public string? Error { get; init; }
}
