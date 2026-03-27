namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Result of a HandBrakeCLI scan operation.</summary>
public class HandBrakeScanResult
{
	public required bool IsSuccess { get; init; }
	public HandBrakeError? Error { get; init; }
	public required string InputPath { get; init; }
	public string? ScanOutputFilePath { get; init; }
	public IReadOnlyList<TitleInfo> Titles { get; init; } = [];
}
