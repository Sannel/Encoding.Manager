using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinMetadataSyncService
{
	Task<IReadOnlyList<JellyfinMetadataServerPair>> GetAllPairsAsync(CancellationToken ct = default);
	Task<JellyfinMetadataServerPair?> GetPairAsync(Guid id, CancellationToken ct = default);
	Task<JellyfinMetadataServerPair> CreatePairAsync(JellyfinMetadataServerPairDto dto, CancellationToken ct = default);
	Task<JellyfinMetadataServerPair?> UpdatePairAsync(Guid id, JellyfinMetadataServerPairDto dto, CancellationToken ct = default);
	Task<bool> DeletePairAsync(Guid id, CancellationToken ct = default);
	Task SyncPairAsync(JellyfinMetadataServerPair pair, IProgress<(int Processed, int Total)>? progress = null, CancellationToken ct = default);
}
