namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>
/// Result of running a process to completion.
/// </summary>
public class ProcessResult
{
	public required int ExitCode { get; init; }
	public required string StandardOutput { get; init; }
	public required string StandardError { get; init; }
}
