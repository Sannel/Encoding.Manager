namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Result of <c>browse_directory</c>.</summary>
public class McpBrowseResult
{
	public required string Root { get; init; }

	/// <summary>Root-relative path that was browsed ("" for the root itself).</summary>
	public required string Path { get; init; }

	public required IReadOnlyList<McpDirectoryDto> Directories { get; init; }

	public required IReadOnlyList<McpFileDto> Files { get; init; }
}
