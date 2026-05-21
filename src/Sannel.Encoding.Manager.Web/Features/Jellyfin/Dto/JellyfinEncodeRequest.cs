namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public class JellyfinEncodeRequest
{
	public Guid ServerId { get; set; }
	public string ItemId { get; set; } = string.Empty;
	public string PresetLabel { get; set; } = string.Empty;
	/// <summary>Destination Jellyfin server to refresh after upload. Null for local encode destination.</summary>
	public Guid? DestServerId { get; set; }
	/// <summary>Destination root for SFTP upload. Null when encoding to the runner's local TrackDestinationRoot.</summary>
	public Guid? DestRootId { get; set; }
}
