namespace Sannel.Encoding.Manager.Web.Features.Scan.Dto;

/// <summary>Outcome of <see cref="Services.IEncodeJobSubmissionService.SubmitAsync"/>.</summary>
public class EncodeJobSubmissionResult
{
	public bool Accepted { get; init; }

	/// <summary>Id of the created queue item when <see cref="Accepted"/> is true.</summary>
	public Guid? QueueItemId { get; init; }

	/// <summary>Number of tracks that were queued.</summary>
	public int TrackCount { get; init; }

	/// <summary>Number of tracks dropped because their output name was blank.</summary>
	public int SkippedTrackCount { get; init; }

	/// <summary>Why the submission was rejected, when <see cref="Accepted"/> is false.</summary>
	public string? RejectionReason { get; init; }

	public static EncodeJobSubmissionResult Rejected(string reason, int skipped) =>
		new() { Accepted = false, RejectionReason = reason, SkippedTrackCount = skipped };
}
