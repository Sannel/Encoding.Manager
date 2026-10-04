using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinSftpService
{
	Task UploadFileAsync(JellyfinServer destServer, string localFilePath, string remoteFilePath, CancellationToken ct = default);
}
