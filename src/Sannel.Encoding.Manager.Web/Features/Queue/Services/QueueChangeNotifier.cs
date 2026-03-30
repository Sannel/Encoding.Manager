using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Services;

/// <summary>
/// In-process pub/sub notifier for queue changes.
/// Blazor Server components subscribe to these events instead of using a SignalR client
/// connection (which would require the server to connect back to itself with auth credentials).
/// </summary>
public class QueueChangeNotifier
{
	/// <summary>Raised when a queue item is added or updated.</summary>
	public event Action<EncodeQueueItem>? ItemUpserted;

	/// <summary>Raised when a queue item is deleted.</summary>
	public event Action<Guid>? ItemDeleted;

	public void NotifyItemUpserted(EncodeQueueItem item) => ItemUpserted?.Invoke(item);

	public void NotifyItemDeleted(Guid id) => ItemDeleted?.Invoke(id);
}
