namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

public class JellyfinServer
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string BaseUrl { get; set; } = string.Empty;
	public string ApiKey { get; set; } = string.Empty;
	public bool IsSource { get; set; }
	public bool IsDestination { get; set; }
	public string? SftpHost { get; set; }
	public int SftpPort { get; set; } = 22;
	public string? SftpUsername { get; set; }
	public string? SftpPassword { get; set; }
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
