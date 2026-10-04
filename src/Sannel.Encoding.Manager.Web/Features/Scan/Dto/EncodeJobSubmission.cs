using Sannel.Encoding.Manager.Web.Features.Queue.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Dto;

/// <summary>
/// Everything needed to create one disk-level encode queue item. Shared by the Scan page and the MCP server
/// so both produce identical queue items.
/// </summary>
public class EncodeJobSubmission
{
	/// <summary>Configured filesystem root label. Null when <see cref="DiscPath"/> is absolute.</summary>
	public string? RootLabel { get; init; }

	/// <summary>Root-relative (forward-slash) path to the disc folder or media folder.</summary>
	public required string DiscPath { get; init; }

	/// <summary>"Titles", "Chapters" or "Files".</summary>
	public required string Mode { get; init; }

	/// <summary>Preset label stamped on every track. Null means no preset.</summary>
	public string? PresetLabel { get; init; }

	public int? TvdbId { get; init; }

	public string? TvdbShowName { get; init; }

	public required IReadOnlyList<EncodeTrackConfig> Tracks { get; init; }

	/// <summary>Display name of the creating user.</summary>
	public string? CreatedBy { get; init; }

	/// <summary>Entra object id of the creating user.</summary>
	public string? CreatedByObjectId { get; init; }

	/// <summary>"UI" or "MCP".</summary>
	public required string CreatedVia { get; init; }
}
