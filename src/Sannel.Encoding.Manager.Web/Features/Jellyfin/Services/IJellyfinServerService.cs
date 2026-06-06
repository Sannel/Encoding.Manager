using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinServerService
{
	Task<IReadOnlyList<JellyfinServer>> GetAllServersAsync(CancellationToken ct = default);
	Task<JellyfinServer?> GetServerAsync(Guid id, CancellationToken ct = default);
	Task<JellyfinServer> CreateServerAsync(JellyfinServerDto dto, CancellationToken ct = default);
	Task<JellyfinServer?> UpdateServerAsync(Guid id, JellyfinServerDto dto, CancellationToken ct = default);
	Task<bool> DeleteServerAsync(Guid id, CancellationToken ct = default);

	Task<IReadOnlyList<JellyfinDestinationRoot>> GetDestinationRootsAsync(Guid serverId, CancellationToken ct = default);
	Task<JellyfinDestinationRoot> CreateDestinationRootAsync(JellyfinDestinationRootDto dto, CancellationToken ct = default);
	Task<JellyfinDestinationRoot?> UpdateDestinationRootAsync(Guid id, JellyfinDestinationRootDto dto, CancellationToken ct = default);
	Task<bool> DeleteDestinationRootAsync(Guid id, CancellationToken ct = default);
}
