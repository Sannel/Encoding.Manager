namespace Sannel.Encoding.Manager.Web.Features.Runner.Dto;

/// <summary>Response returned when a runner successfully claims a job.</summary>
public class ClaimedJobResponse
{
	public Guid JobId { get; set; }
	public string DiscPath { get; set; } = string.Empty;
	public string? DiscRootLabel { get; set; }
	public string Mode { get; set; } = string.Empty;
	public string? TvdbShowName { get; set; }
	public int? TvdbId { get; set; }
	public string AudioDefault { get; set; } = string.Empty;
	public string TracksJson { get; set; } = "[]";

	/// <summary>
	/// Map of preset label -> { RootLabel, RelativePath }.
	/// Runner resolves each preset file from its own local filesystem roots.
	/// </summary>
	public Dictionary<string, PresetLocation> PresetMap { get; set; } = [];

	/// <summary>Output path template with variable placeholders.</summary>
	public string TrackDestinationTemplate { get; set; } = string.Empty;

	/// <summary>Filesystem root label for the output directory.</summary>
	public string? TrackDestinationRoot { get; set; }

	/// <summary>Comma-separated ISO 639-2 audio language codes.</summary>
	public string[] AudioLanguages { get; set; } = [];

	/// <summary>Comma-separated ISO 639-2 subtitle language codes.</summary>
	public string[] SubtitleLanguages { get; set; } = [];
}

/// <summary>Location of a preset file relative to a filesystem root.</summary>
public class PresetLocation
{
	public string PresetName { get; set; } = string.Empty;
	public string RootLabel { get; set; } = string.Empty;
	public string RelativePath { get; set; } = string.Empty;
}
