using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sannel.Encoding.Manager.Web.Features.Runner.Services;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Hubs;

[Authorize(Policy = "RunnerApi")]
public class RunnerStatusHub : Hub
{
	private readonly IRunnerJobService _runnerJobService;

	public RunnerStatusHub(IRunnerJobService runnerJobService)
	{
		_runnerJobService = runnerJobService;
	}

	public async Task UpdateJobStatus(Guid jobId, string status, int? progressPercent = null, int? currentTrackProgressPercent = null, string? error = null, string? encodingCommand = null)
	{
		var updated = await _runnerJobService.UpdateJobStatusAsync(jobId, status, progressPercent, currentTrackProgressPercent, error, encodingCommand, Context.ConnectionAborted);
		if (!updated)
		{
			throw new HubException($"Queue item '{jobId}' was not found.");
		}
	}
}