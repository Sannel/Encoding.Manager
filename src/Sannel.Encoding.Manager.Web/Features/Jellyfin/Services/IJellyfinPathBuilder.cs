using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinPathBuilder
{
	string BuildRemotePath(JellyfinDestinationRoot root, JellyfinItem item, string extension = "mkv");
}
