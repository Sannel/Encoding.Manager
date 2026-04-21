using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinSyncService
{
	Task<IReadOnlyList<JellyfinSyncProfile>> GetAllSyncProfilesAsync(CancellationToken ct = default);
	Task<JellyfinSyncProfile?> GetSyncProfileAsync(Guid id, CancellationToken ct = default);
	Task<JellyfinSyncProfile> CreateSyncProfileAsync(JellyfinSyncProfileDto dto, CancellationToken ct = default);
	Task<JellyfinSyncProfile?> UpdateSyncProfileAsync(Guid id, JellyfinSyncProfileDto dto, CancellationToken ct = default);
	Task<bool> DeleteSyncProfileAsync(Guid id, CancellationToken ct = default);
	Task SyncProfileAsync(JellyfinSyncProfile profile, IProgress<(int Processed, int Total)>? progress = null, CancellationToken ct = default);
}
