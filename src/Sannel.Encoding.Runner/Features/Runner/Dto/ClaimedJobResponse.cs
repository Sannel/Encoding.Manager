namespace Sannel.Encoding.Runner.Features.Runner.Dto;

/// <summary>Response from the claim-next API endpoint.</summary>
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
	public Dictionary<string, PresetLocation> PresetMap { get; set; } = [];
	public string TrackDestinationTemplate { get; set; } = string.Empty;
	public string? TrackDestinationRoot { get; set; }
	public string[] AudioLanguages { get; set; } = [];
	public string[] SubtitleLanguages { get; set; } = [];

	/// <summary>Jellyfin download URL when the source is a Jellyfin server. Null for local disc jobs.</summary>
	public string? JellyfinDownloadUrl { get; set; }

	/// <summary>Decrypted Jellyfin API key for downloading the source item.</summary>
	public string? JellyfinApiKey { get; set; }

	/// <summary>Destination SFTP host for Jellyfin uploads.</summary>
	public string? JellyfinSftpHost { get; set; }

	/// <summary>Destination SFTP port for Jellyfin uploads.</summary>
	public int? JellyfinSftpPort { get; set; }

	/// <summary>Destination SFTP username for Jellyfin uploads.</summary>
	public string? JellyfinSftpUsername { get; set; }

	/// <summary>Decrypted destination SFTP password for Jellyfin uploads.</summary>
	public string? JellyfinSftpPassword { get; set; }

	/// <summary>Destination SFTP path (including file name) for Jellyfin uploads.</summary>
	public string? JellyfinSftpRemotePath { get; set; }
}

/// <summary>Location of a preset file relative to a filesystem root.</summary>
public class PresetLocation
{
	public string PresetName { get; set; } = string.Empty;
	public string RootLabel { get; set; } = string.Empty;
	public string RelativePath { get; set; } = string.Empty;
}

/// <summary>Response from the runner enabled endpoint.</summary>
public class RunnerStatusResponse
{
	public bool IsEnabled { get; set; }
}
