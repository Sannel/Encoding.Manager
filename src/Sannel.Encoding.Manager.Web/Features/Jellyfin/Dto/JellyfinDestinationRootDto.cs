namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

public class JellyfinDestinationRootDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid ServerId { get; set; }
	public string RootPath { get; set; } = string.Empty;
}
