namespace Sannel.Encoding.Runner.Features.Runner.Dto;

/// <summary>Response from the claim-next API endpoint.</summary>
public class ClaimedJobResponse
{
	public Guid JobId { get; set; }
	public string DiscPath { get; set; } = string.Empty;
	public string? DiscRootLabel { get; set; }
	public string Mode { get; set; } = string.Empty;
	public string? TvdbShowName { get; set; }
	public string AudioDefault { get; set; } = string.Empty;
	public string TracksJson { get; set; } = "[]";
	public Dictionary<string, PresetLocation> PresetMap { get; set; } = [];
	public string TrackDestinationTemplate { get; set; } = string.Empty;
	public string? TrackDestinationRoot { get; set; }
	public string[] AudioLanguages { get; set; } = [];
	public string[] SubtitleLanguages { get; set; } = [];
}

/// <summary>Location of a preset file relative to a filesystem root.</summary>
public class PresetLocation
{
	public string RootLabel { get; set; } = string.Empty;
	public string RelativePath { get; set; } = string.Empty;
}

/// <summary>Response from the runner enabled endpoint.</summary>
public class RunnerStatusResponse
{
	public bool IsEnabled { get; set; }
}
