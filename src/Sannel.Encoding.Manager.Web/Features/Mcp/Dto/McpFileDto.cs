namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A media file returned by <c>browse_directory</c> or <c>list_folder_media_files</c>.</summary>
public class McpFileDto
{
	public required string Name { get; init; }

	/// <summary>Path relative to the browsed folder (use as <c>sourceRelativePath</c> when queuing a folder).</summary>
	public required string RelativePath { get; init; }

	public required long SizeBytes { get; init; }
}
