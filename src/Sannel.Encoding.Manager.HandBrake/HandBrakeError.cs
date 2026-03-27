namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Structured failure details from HandBrakeCLI.</summary>
public class HandBrakeError
{
	public int ExitCode { get; init; }
	public string Message { get; init; } = string.Empty;
	public string RawOutput { get; init; } = string.Empty;
}
