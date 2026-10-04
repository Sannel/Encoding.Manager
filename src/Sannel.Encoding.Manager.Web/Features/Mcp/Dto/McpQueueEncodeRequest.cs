namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Input of <c>queue_encode_job</c>.</summary>
public class McpQueueEncodeRequest
{
	/// <summary>Configured root label.</summary>
	public string Root { get; set; } = string.Empty;

	/// <summary>Root-relative path of the disc folder, media folder, or single file.</summary>
	public string? Path { get; set; }

	/// <summary>"disc", "folder" or "file".</summary>
	public string Selection { get; set; } = string.Empty;

	/// <summary>"Titles" or "Chapters" — required for selection "disc", ignored otherwise.</summary>
	public string? Mode { get; set; }

	/// <summary>Preset label from <c>list_presets</c>. Null for no preset.</summary>
	public string? PresetLabel { get; set; }

	/// <summary>TVDB series id; the series name is looked up and stored with the job.</summary>
	public int? TvdbSeriesId { get; set; }

	/// <summary>Movie release year, applied to every track.</summary>
	public string? MovieYear { get; set; }

	public List<McpQueueTrack> Tracks { get; set; } = [];
}
