namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>Result of a HandBrakeCLI encode operation.</summary>
public class HandBrakeEncodeResult
{
	public required bool IsSuccess { get; init; }
	public HandBrakeError? Error { get; init; }
	public required string OutputPath { get; init; }
	public TimeSpan ElapsedTime { get; init; }
	public double AverageFps { get; init; }
}
