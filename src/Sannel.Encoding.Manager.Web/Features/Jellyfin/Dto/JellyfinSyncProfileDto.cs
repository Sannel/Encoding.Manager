namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public class JellyfinSyncProfileDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid ServerAId { get; set; }
	public string UserIdA { get; set; } = string.Empty;
	public Guid ServerBId { get; set; }
	public string UserIdB { get; set; } = string.Empty;
	public bool IsEnabled { get; set; } = true;
	public int SyncIntervalMinutes { get; set; } = 60;
}
