namespace Sannel.Encoding.Manager.Web.Features.Runner.Dto;

/// <summary>Request body for updating a job's status or progress.</summary>
public class UpdateStatusRequest
{
	/// <summary>"Encoding", "Finished", or "Failed".</summary>
	public string Status { get; set; } = string.Empty;

	/// <summary>Error message (when Status == "Failed").</summary>
	public string? Error { get; set; }

	/// <summary>Encoding progress percentage (0-100).</summary>
	public int? ProgressPercent { get; set; }
}
