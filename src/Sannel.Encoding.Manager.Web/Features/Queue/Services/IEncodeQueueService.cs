using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public enum MoveDirection
{
	Up,
	Down,
}

public interface IEncodeQueueService
{
	Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default);
	Task<IReadOnlyList<EncodeQueueItem>> GetPagedItemsAsync(int skip, int take, bool includeCleared = false, CancellationToken ct = default);
	Task DeleteItemAsync(Guid id, CancellationToken ct = default);
	Task UpdateTracksAsync(Guid id, List<EncodeTrackConfig> tracks, CancellationToken ct = default);
	Task<bool> ResetToQueuedAsync(Guid id, CancellationToken ct = default);
	Task<bool> CancelEncodingAsync(Guid id, CancellationToken ct = default);
	Task<int> ClearFinishedAsync(CancellationToken ct = default);
	Task<bool> MoveItemAsync(Guid id, MoveDirection direction, CancellationToken ct = default);
	Task<bool> MoveToIndexAsync(Guid id, int targetIndex, CancellationToken ct = default);
}
