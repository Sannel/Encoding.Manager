namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A media file returned by <c>browse_directory</c> or <c>list_folder_media_files</c>.</summary>
public class McpFileDto
{
	public required string Name { get; init; }

	/// <summary>Path relative to the browsed folder (use as <c>sourceRelativePath</c> when queuing a folder).</summary>
	public required string RelativePath { get; init; }

	public required long SizeBytes { get; init; }

	/// <summary>
	/// Interlace verdict (<c>list_folder_media_files</c> only): "interlaced", "telecined", "mixed", "progressive",
	/// "pending" (probe running — call again) or "unknown". Null where not checked (browse_directory).
	/// </summary>
	public string? Interlace { get; init; }

	public double? InterlacedPercent { get; init; }

	public double? TelecinePercent { get; init; }

	/// <summary>Preset this file's verdict suggests; null while pending or unknown. Use it as the track's presetLabel.</summary>
	public string? RecommendedPreset { get; init; }
}
