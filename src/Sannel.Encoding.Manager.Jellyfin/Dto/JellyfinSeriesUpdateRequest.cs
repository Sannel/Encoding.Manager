using Sannel.Encoding.Manager.Jellyfin.Dto;

namespace Sannel.Encoding.Manager.Jellyfin.Dto;

/// <summary>
/// Write model for updating Series metadata on a Jellyfin server.
/// Mirrors the fields Jellyfin's POST /Items/{itemId} endpoint expects.
/// Populated by GETting the current item and merging only the fields we intend to change.
/// </summary>
public class JellyfinSeriesUpdateRequest
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public int? ProductionYear { get; set; }
	public JellyfinProviderIds? ProviderIds { get; set; }
	public string? Overview { get; set; }
	public string[]? Genres { get; set; }
	public string? OfficialRating { get; set; }
	public string? Status { get; set; }
	public JellyfinStudio[]? Studios { get; set; }
	public string? PremiereDate { get; set; }
	public string? EndDate { get; set; }
	public string[]? Tags { get; set; }
	public string? DisplayOrder { get; set; }
}
