namespace Sannel.Encoding.Manager.Jellyfin.Dto;

public class GetItemsRequest
{
	public string? IncludeItemTypes { get; set; }
	public bool Recursive { get; set; } = true;
	public string Fields { get; set; } = "ProviderIds,UserData,SeriesProviderIds,Path";
	public string? ParentId { get; set; }
	public string? SearchTerm { get; set; }
	public int? StartIndex { get; set; }
	public int? Limit { get; set; }
	public string? AnyProviderIdEquals { get; set; }
}
