namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public class JellyfinEncodeRequest
{
	public Guid ServerId { get; set; }
	public string ItemId { get; set; } = string.Empty;
	public string PresetLabel { get; set; } = string.Empty;
	public Guid DestServerId { get; set; }
	public Guid DestRootId { get; set; }
}
