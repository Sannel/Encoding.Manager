using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Result of <c>inspect_disc_menus</c> / <c>get_disc_menu_status</c>.</summary>
public class McpDiscMenuResult
{
	/// <summary>"Completed", "Inspecting", "Failed" or "Unavailable".</summary>
	public required string Status { get; init; }

	public DateTimeOffset? StartedAt { get; init; }

	/// <summary>When <see cref="Status"/> is "Inspecting": wait this long, then call <c>get_disc_menu_status</c>.</summary>
	public int? PollAfterSeconds { get; init; }

	public string? Error { get; init; }

	/// <summary>The menu map when completed.</summary>
	public DiscMenuMap? MenuMap { get; init; }
}
