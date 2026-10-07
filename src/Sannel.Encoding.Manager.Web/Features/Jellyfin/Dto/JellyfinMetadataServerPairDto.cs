namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public class JellyfinMetadataServerPairDto
{
	public Guid SourceServerId { get; set; }
	public Guid DestinationServerId { get; set; }
	public bool IsEnabled { get; set; } = true;
}
