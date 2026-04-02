using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sannel.Encoding.Manager.Web.Features.Runner.Dto;
using Sannel.Encoding.Manager.Web.Features.Runner.Services;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Controllers;

/// <summary>
/// API controller for encoding runner operations.
/// </summary>
[ApiController]
[Route("api/runner")]
[Authorize(Policy = "RunnerApi")]
public class RunnerController : ControllerBase
{
	private readonly IRunnerJobService _runnerJobService;
	private readonly ILogger<RunnerController> _logger;

	public RunnerController(IRunnerJobService runnerJobService, ILogger<RunnerController> logger)
	{
		_runnerJobService = runnerJobService;
		_logger = logger;
	}

	/// <summary>Runner heartbeat — upserts runner and updates LastSeenAt.</summary>
	[HttpPost("heartbeat")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Heartbeat([FromBody] ClaimNextRequest request, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.RunnerName))
		{
			return BadRequest(new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Invalid request",
				Detail = "RunnerName is required."
			});
		}

		await _runnerJobService.RegisterOrUpdateRunnerAsync(request.RunnerName, ct);
		return Ok();
	}

	/// <summary>Check whether the named runner is enabled.</summary>
	[HttpGet("{name}/enabled")]
	[ProducesResponseType(typeof(RunnerStatusResponse), StatusCodes.Status200OK)]
	public async Task<ActionResult<RunnerStatusResponse>> GetEnabled(string name, CancellationToken ct)
	{
		var isEnabled = await _runnerJobService.IsEnabledAsync(name, ct);
		return Ok(new RunnerStatusResponse { IsEnabled = isEnabled });
	}

	/// <summary>Returns whether cancel was requested for the runner's current job.</summary>
	[HttpGet("{name}/jobs/{jobId:guid}/cancel-requested")]
	[ProducesResponseType(typeof(CancelRequestResponse), StatusCodes.Status200OK)]
	public async Task<ActionResult<CancelRequestResponse>> GetCancelRequested(string name, Guid jobId, CancellationToken ct)
	{
		var cancelRequested = await _runnerJobService.IsCancelRequestedAsync(name, jobId, ct);
		return Ok(new CancelRequestResponse { CancelRequested = cancelRequested });
	}

	/// <summary>
	/// Atomically claim the next queued job for the given runner.
	/// Returns 204 if nothing is available.
	/// </summary>
	[HttpPost("claim-next")]
	[ProducesResponseType(typeof(ClaimedJobResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ClaimNext([FromBody] ClaimNextRequest request, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.RunnerName))
		{
			return BadRequest(new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Invalid request",
				Detail = "RunnerName is required."
			});
		}

		var job = await _runnerJobService.ClaimNextJobAsync(request.RunnerName, ct);
		if (job is null)
		{
			return NoContent();
		}

		return Ok(job);
	}

	/// <summary>Update job status and/or progress.</summary>
	[HttpPut("items/{id:guid}/status")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.Status))
		{
			return BadRequest(new ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Invalid request",
				Detail = "Status is required."
			});
		}

		var updated = await _runnerJobService.UpdateJobStatusAsync(id, request.Status, request.ProgressPercent, request.CurrentTrackProgressPercent, request.Error, request.EncodingCommand, ct);
		if (!updated)
		{
			return NotFound();
		}

		return Ok();
	}
}
