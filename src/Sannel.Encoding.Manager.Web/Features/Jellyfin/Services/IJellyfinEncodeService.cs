using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public interface IJellyfinEncodeService
{
	Task<EncodeQueueItem> QueueItemAsync(JellyfinEncodeRequest request, CancellationToken ct = default);
	Task HandleEncodeCompletedAsync(Guid queueItemId, CancellationToken ct = default);
}
