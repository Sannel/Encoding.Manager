namespace Sannel.Encoding.Manager.Jellyfin.Dto;

public class JellyfinVirtualFolder
{
	public string Name { get; set; } = string.Empty;
	public string[] Locations { get; set; } = [];
	public string? CollectionType { get; set; }
	public string? ItemId { get; set; }
}
