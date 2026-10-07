namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A HandBrake preset that can be used as <c>presetLabel</c>.</summary>
public class McpPresetDto
{
	public required string Label { get; init; }

	public required string PresetName { get; init; }
}
