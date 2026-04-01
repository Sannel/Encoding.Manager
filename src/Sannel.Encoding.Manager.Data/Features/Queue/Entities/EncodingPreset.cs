namespace Sannel.Encoding.Manager.Web.Features.Queue.Entities;

/// <summary>A named HandBrake preset JSON file stored on the server.</summary>
public class EncodingPreset
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>User-facing display label.</summary>
	public string Label { get; set; } = string.Empty;

	/// <summary>The preset name as it appears in the JSON file (e.g. "Fast 1080p30").</summary>
	public string PresetName { get; set; } = string.Empty;

	/// <summary>The filesystem root label (matches a configured root).</summary>
	public string RootLabel { get; set; } = string.Empty;

	/// <summary>Path of the .json file relative to <see cref="RootLabel"/>.</summary>
	public string RelativePath { get; set; } = string.Empty;
}
