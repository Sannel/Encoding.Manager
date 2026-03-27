namespace Sannel.Encoding.Manager.Web.Features.Runner.Dto;

/// <summary>Request body for claiming the next available job.</summary>
public class ClaimNextRequest
{
	/// <summary>Name of the runner claiming the job.</summary>
	public string RunnerName { get; set; } = string.Empty;
}
