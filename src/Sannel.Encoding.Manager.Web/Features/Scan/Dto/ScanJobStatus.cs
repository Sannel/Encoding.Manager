using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Dto;

/// <summary>Snapshot of a background HandBrake disc scan.</summary>
public class ScanJobStatus
{
	public required BackgroundJobState State { get; init; }

	/// <summary>The physical path being scanned.</summary>
	public required string PhysicalPath { get; init; }

	public required DateTimeOffset StartedAt { get; init; }

	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>The scan result when <see cref="State"/> is <see cref="BackgroundJobState.Completed"/>.</summary>
	public HandBrakeScanResult? Result { get; init; }

	/// <summary>The failure message when <see cref="State"/> is <see cref="BackgroundJobState.Failed"/>.</summary>
	public string? Error { get; init; }
}
