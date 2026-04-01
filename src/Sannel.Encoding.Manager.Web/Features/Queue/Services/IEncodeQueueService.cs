using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public interface IEncodeQueueService
{
	Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default);
	Task<IReadOnlyList<EncodeQueueItem>> GetItemsAsync(bool includeCleared = false, CancellationToken ct = default);
	Task DeleteItemAsync(Guid id, CancellationToken ct = default);
	Task UpdateTracksAsync(Guid id, List<EncodeTrackConfig> tracks, CancellationToken ct = default);
	Task<bool> ResetToQueuedAsync(Guid id, CancellationToken ct = default);
	Task<int> ClearFinishedAsync(CancellationToken ct = default);
}
