using Sannel.Encoding.Runner.Features.Runner.Dto;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

/// <summary>Client for communicating with the Encoding Manager web API.</summary>
public interface IRunnerApiClient
{
	Task SendHeartbeatAsync(string name, CancellationToken ct = default);
	Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);
	Task<ClaimedJobResponse?> ClaimNextJobAsync(string name, CancellationToken ct = default);
	Task UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent = null, string? error = null, CancellationToken ct = default);
}
