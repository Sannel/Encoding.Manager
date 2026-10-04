namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>One track of a <c>queue_encode_job</c> request.</summary>
public class McpQueueTrack
{
	/// <summary>HandBrake title number (disc selections).</summary>
	public int? TitleNumber { get; set; }

	/// <summary>First chapter (mode "Chapters").</summary>
	public int? StartChapter { get; set; }

	/// <summary>Last chapter, inclusive (mode "Chapters").</summary>
	public int? EndChapter { get; set; }

	/// <summary>File path relative to the selected folder (selection "folder"). Ignored for "file".</summary>
	public string? SourceRelativePath { get; set; }

	/// <summary>Output file name without extension. Blank = skip this track.</summary>
	public string OutputName { get; set; } = string.Empty;

	public int? SeasonNumber { get; set; }

	public int? EpisodeNumber { get; set; }

	/// <summary>Movie resolution ("480p", "720p", "1080p", "4k"). Omit for TV.</summary>
	public string? Resolution { get; set; }
}
