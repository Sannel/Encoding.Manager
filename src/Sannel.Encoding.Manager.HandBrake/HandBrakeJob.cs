namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Describes an encoding request to HandBrakeCLI.</summary>
public class HandBrakeJob
{
	/// <summary>Absolute path to the source file.</summary>
	public required string InputPath { get; init; }

	/// <summary>Absolute path for the output file.</summary>
	public required string OutputPath { get; init; }

	/// <summary>HandBrake preset name (e.g. "Fast 1080p30"). Required when <see cref="PresetFilePath"/> is set (to tell HandBrake which preset to use from the imported file). Can also be used standalone for built-in presets.</summary>
	public string? PresetName { get; init; }

	/// <summary>Path to a .json preset file to import. When set, <see cref="PresetName"/> must also be set to the preset name inside the JSON file.</summary>
	public string? PresetFilePath { get; init; }

	/// <summary>Raw extra CLI arguments appended verbatim. Caller is responsible for sanitizing.</summary>
	public string? AdditionalArgs { get; init; }
}
