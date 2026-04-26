namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

public class JellyfinDestinationRoot
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public Guid ServerId { get; set; }
	public JellyfinServer? Server { get; set; }
	public string RootPath { get; set; } = string.Empty;
}
