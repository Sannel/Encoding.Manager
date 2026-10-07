using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sannel.Encoding.Manager.Web.Features.Runner.Services;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Hubs;

[Authorize(Policy = "RunnerApi")]
public class RunnerStatusHub : Hub
{
	private readonly IRunnerJobService _runnerJobService;
	private readonly ILogger<RunnerStatusHub> _logger;

	public RunnerStatusHub(IRunnerJobService runnerJobService, ILogger<RunnerStatusHub> logger)
	{
		_runnerJobService = runnerJobService;
		_logger = logger;
	}

	public async Task UpdateJobStatus(Guid jobId, string status, int? progressPercent = null, int? currentTrackProgressPercent = null, string? error = null, string? encodingCommand = null)
	{
		var updated = await _runnerJobService.UpdateJobStatusAsync(jobId, status, progressPercent, currentTrackProgressPercent, error, encodingCommand, Context.ConnectionAborted);
		if (!updated)
		{
			// The item was deleted while a runner was working on it. The runner's next cancel-requested poll tells it
			// to stop; until then its progress reports have nothing to update and are ignored.
			_logger.LogDebug("Ignored status '{Status}' for deleted queue item {JobId}.", status, jobId);
		}
	}
}
