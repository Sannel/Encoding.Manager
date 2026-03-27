namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Describes an encoding request to HandBrakeCLI.</summary>
public class HandBrakeJob
{
	/// <summary>Absolute path to the source file.</summary>
	public required string InputPath { get; init; }

	/// <summary>Absolute path for the output file.</summary>
	public required string OutputPath { get; init; }

	/// <summary>HandBrake preset name (e.g. "Fast 1080p30"). Either this or <see cref="PresetFilePath"/> must be set.</summary>
	public string? PresetName { get; init; }

	/// <summary>Path to a .json preset file. Takes precedence over <see cref="PresetName"/> if both are set.</summary>
	public string? PresetFilePath { get; init; }

	/// <summary>Raw extra CLI arguments appended verbatim. Caller is responsible for sanitizing.</summary>
	public string? AdditionalArgs { get; init; }
}
