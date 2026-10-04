namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A subtitle track of a scanned title.</summary>
public class McpSubtitleDto
{
	public int TrackNumber { get; init; }

	public string Language { get; init; } = string.Empty;

	public string Format { get; init; } = string.Empty;
}
