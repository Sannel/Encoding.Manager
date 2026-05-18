namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

public class JellyfinMetadataServerPair
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid SourceServerId { get; set; }
	public JellyfinServer? SourceServer { get; set; }
	public Guid DestinationServerId { get; set; }
	public JellyfinServer? DestinationServer { get; set; }
	public bool IsEnabled { get; set; } = true;
	public DateTimeOffset? LastSyncedAt { get; set; }
	public string? LastSyncStatus { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
