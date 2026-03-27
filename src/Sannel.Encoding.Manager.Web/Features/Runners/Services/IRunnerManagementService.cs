using Sannel.Encoding.Manager.Web.Features.Runners.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Runners.Services;

/// <summary>Service for managing runners from the web UI.</summary>
public interface IRunnerManagementService
{
	Task<IReadOnlyList<RunnerDto>> GetRunnersAsync(CancellationToken ct = default);
	Task SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default);
	Task DeleteRunnerAsync(Guid id, CancellationToken ct = default);
}
