namespace Sannel.Encoding.Manager.Jellyfin.Dto;

public class ItemsResponse
{
	public JellyfinItem[] Items { get; set; } = [];
	public int TotalRecordCount { get; set; }
}
