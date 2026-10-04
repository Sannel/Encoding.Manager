namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>An audio track of a scanned title.</summary>
public class McpAudioTrackDto
{
	public int TrackNumber { get; init; }

	public string Language { get; init; } = string.Empty;

	public string Codec { get; init; } = string.Empty;

	public string ChannelLayout { get; init; } = string.Empty;
}
