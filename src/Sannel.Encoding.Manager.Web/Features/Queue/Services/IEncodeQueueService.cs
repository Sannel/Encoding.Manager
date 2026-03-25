using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

public interface IEncodeQueueService
{
	Task AddItemAsync(EncodeQueueItem item, CancellationToken ct = default);
	Task<IReadOnlyList<EncodeQueueItem>> GetItemsAsync(CancellationToken ct = default);
	Task DeleteItemAsync(Guid id, CancellationToken ct = default);
	Task UpdateTracksAsync(Guid id, List<EncodeTrackConfig> tracks, CancellationToken ct = default);
}
