namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A subdirectory returned by <c>browse_directory</c>.</summary>
public class McpDirectoryDto
{
	public required string Name { get; init; }

	/// <summary>"None", "DVD" or "BluRay". A disc folder is selected as a whole (selection "disc").</summary>
	public required string DiscType { get; init; }
}
