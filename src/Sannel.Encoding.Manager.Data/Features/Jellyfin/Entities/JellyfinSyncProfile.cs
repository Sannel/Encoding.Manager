namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

public class JellyfinSyncProfile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public Guid ServerAId { get; set; }
	public JellyfinServer? ServerA { get; set; }
	public string UserIdA { get; set; } = string.Empty;
	public Guid ServerBId { get; set; }
	public JellyfinServer? ServerB { get; set; }
	public string UserIdB { get; set; } = string.Empty;
	public bool IsEnabled { get; set; } = true;
	public int SyncIntervalMinutes { get; set; } = 60;
	public DateTimeOffset? LastSyncedAt { get; set; }
	public string? LastSyncStatus { get; set; }
}
