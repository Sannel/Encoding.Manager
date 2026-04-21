namespace Sannel.Encoding.Manager.Jellyfin.Dto;

public class JellyfinItem
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public int? ProductionYear { get; set; }
	public JellyfinProviderIds? ProviderIds { get; set; }
	public JellyfinUserData? UserData { get; set; }
	public int? IndexNumber { get; set; }
	public int? ParentIndexNumber { get; set; }
	public string? SeriesName { get; set; }
	public string? SeriesId { get; set; }
	public JellyfinProviderIds? SeriesProviderIds { get; set; }
	public string? ParentId { get; set; }
	public string? Path { get; set; }
}
