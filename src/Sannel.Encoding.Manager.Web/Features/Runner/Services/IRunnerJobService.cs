using Sannel.Encoding.Manager.Web.Features.Runner.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Services;

/// <summary>Service for runner job operations: registration, claiming, and status updates.</summary>
public interface IRunnerJobService
{
	/// <summary>Upsert a runner row and update LastSeenAt.</summary>
	Task RegisterOrUpdateRunnerAsync(string name, CancellationToken ct = default);

	/// <summary>Returns whether the named runner is enabled.</summary>
	Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);

	/// <summary>
	/// Atomically claim the next "Queued" item for the given runner.
	/// Returns a populated <see cref="ClaimedJobResponse"/> or null if nothing is queued.
	/// </summary>
	Task<ClaimedJobResponse?> ClaimNextJobAsync(string runnerName, CancellationToken ct = default);

	/// <summary>
	/// Update job status and/or progress.
	/// For "Encoding": updates ProgressPercent only.
	/// For "Finished"/"Failed": sets Status, CompletedAt, clears ProgressPercent.
	/// </summary>
	Task<bool> UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent, string? error, CancellationToken ct = default);
}
