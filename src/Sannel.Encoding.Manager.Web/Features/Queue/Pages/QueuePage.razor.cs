using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Queue.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Options;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Pages;

public partial class QueuePage : ComponentBase, IAsyncDisposable
{
	[Inject]
	private IEncodeQueueService EncodeQueueService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private QueueChangeNotifier Notifier { get; set; } = default!;

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Inject]
	private IOptions<QueueOptions> QueueOptions { get; set; } = default!;

	private List<EncodeQueueItem> _items = [];
	private bool _isLoading = true;
	private bool _showCleared;
	private MudDropContainer<EncodeQueueItem>? _dropContainer;

	private int _skip;
	private bool _hasMore = true;
	private bool _isLoadingMore;
	private int _loadGeneration;
	private ElementReference _scrollSentinel;
	private IJSObjectReference? _jsModule;
	private IJSObjectReference? _jsObserver;
	private DotNetObjectReference<QueuePage>? _dotNetRef;

	private int PageSize => this.QueueOptions.Value.PageSize;

	protected override async Task OnInitializedAsync()
	{
		await this.LoadFirstPageAsync();
		this.Notifier.ItemUpserted += this.OnItemUpserted;
		this.Notifier.ItemDeleted += this.OnItemDeleted;
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			return;
		}

		this._dotNetRef = DotNetObjectReference.Create(this);
		this._jsModule = await this.JS.InvokeAsync<IJSObjectReference>(
			"import", "./Features/Queue/Pages/QueuePage.razor.js");
		this._jsObserver = await this._jsModule.InvokeAsync<IJSObjectReference>(
			"initScrollSentinel", this._scrollSentinel, this._dotNetRef);
	}

	private async Task LoadFirstPageAsync()
	{
		this._isLoading = true;
		var result = await this.EncodeQueueService.GetPagedItemsAsync(0, this.PageSize, this._showCleared);
		this._items = result.ToList();
		this._skip = result.Count;
		this._hasMore = result.Count == this.PageSize;
		this._isLoading = false;
	}

	[JSInvokable]
	public async Task OnScrolledToBottom()
	{
		if (!this._hasMore || this._isLoadingMore)
		{
			return;
		}

		var gen = this._loadGeneration;
		this._isLoadingMore = true;
		await this.InvokeAsync(this.StateHasChanged);

		var result = await this.EncodeQueueService.GetPagedItemsAsync(this._skip, this.PageSize, this._showCleared);

		if (gen != this._loadGeneration)
		{
			// Filter changed while this load was in flight — discard results
			this._isLoadingMore = false;
			return;
		}

		var existingIds = this._items.Select(i => i.Id).ToHashSet();
		foreach (var item in result)
		{
			if (existingIds.Add(item.Id))
			{
				this._items.Add(item);
			}
		}

		this._skip += result.Count;
		this._hasMore = result.Count == this.PageSize;
		this._isLoadingMore = false;
		await this.InvokeAsync(StateHasChanged);
	}

	private async void OnItemUpserted(EncodeQueueItem item)
	{
		await this.InvokeAsync(() =>
		{
			if (!this._showCleared && item.IsArchived)
			{
				this.ApplyItemDeleted(item.Id);
			}
			else
			{
				this.ApplyItemUpsert(item);
			}

			this.StateHasChanged();
			this._dropContainer?.Refresh();
		});
	}

	private async void OnItemDeleted(Guid id)
	{
		await this.InvokeAsync(() =>
		{
			this.ApplyItemDeleted(id);
			this.StateHasChanged();
			this._dropContainer?.Refresh();
		});
	}

	private void ApplyItemUpsert(EncodeQueueItem item)
	{
		if (item.Id == Guid.Empty)
		{
			return;
		}

		var updated = this._items.ToList();
		var existingIndex = updated.FindIndex(existing => existing.Id == item.Id);
		if (existingIndex >= 0)
		{
			updated[existingIndex] = item;
		}
		else
		{
			updated.Add(item);
		}

		this._items = updated
			.OrderBy(i => i.SortOrder)
			.ToList();
	}

	private void ApplyItemDeleted(Guid id)
	{
		if (id == Guid.Empty)
		{
			return;
		}

		this._items = this._items
			.Where(item => item.Id != id)
			.ToList();
	}

	private async Task MoveItemAsync(Guid id, MoveDirection direction)
	{
		await this.EncodeQueueService.MoveItemAsync(id, direction);
	}

	private async Task OnItemDroppedAsync(MudItemDropInfo<EncodeQueueItem> info)
	{
		if (info.Item is null)
		{
			return;
		}

		// info.IndexInZone is relative to the Queued-only drop zone.
		// Walk _items (ordered by SortOrder) to find the absolute index
		// corresponding to the info.IndexInZone-th Queued item.
		var queuedCount = 0;
		var absoluteIndex = this._items.Count - 1;
		for (var i = 0; i < this._items.Count; i++)
		{
			if (string.Equals(this._items[i].Status, "Queued", StringComparison.OrdinalIgnoreCase))
			{
				if (queuedCount == info.IndexInZone)
				{
					absoluteIndex = i;
					break;
				}

				queuedCount++;
			}
		}

		await this.EncodeQueueService.MoveToIndexAsync(info.Item.Id, absoluteIndex);
	}

	private bool CanMoveUp(EncodeQueueItem item)
	{
		if (!string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var currentIndex = -1;
		for (var i = 0; i < this._items.Count; i++)
		{
			if (this._items[i].Id == item.Id)
			{
				currentIndex = i;
				break;
			}
		}

		for (var i = currentIndex - 1; i >= 0; i--)
		{
			if (string.Equals(this._items[i].Status, "Queued", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private bool CanMoveDown(EncodeQueueItem item)
	{
		if (!string.Equals(item.Status, "Queued", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var currentIndex = -1;
		for (var i = 0; i < this._items.Count; i++)
		{
			if (this._items[i].Id == item.Id)
			{
				currentIndex = i;
				break;
			}
		}

		for (var i = currentIndex + 1; i < this._items.Count; i++)
		{
			if (string.Equals(this._items[i].Status, "Queued", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private async Task DeleteItemAsync(Guid id)
	{
		await this.EncodeQueueService.DeleteItemAsync(id);
		this.Snackbar.Add("Item removed from queue.", Severity.Success);
	}

	private async Task RetryFailedItemAsync(Guid id)
	{
		var reset = await this.EncodeQueueService.ResetToQueuedAsync(id);
		if (reset)
		{
			this.Snackbar.Add("Item reset to queued.", Severity.Success);
		}
		else
		{
			this.Snackbar.Add("Only failed, finished, canceled, or cancel-requested items can be reset to queued.", Severity.Warning);
		}
	}

	private async Task CancelEncodingAsync(Guid id)
	{
		var canceled = await this.EncodeQueueService.CancelEncodingAsync(id);
		if (canceled)
		{
			this.Snackbar.Add("Cancellation requested for the active encode.", Severity.Info);
		}
		else
		{
			this.Snackbar.Add("Only actively encoding items can be canceled.", Severity.Warning);
		}
	}

	private async Task ClearFinishedAsync()
	{
		var count = await this.EncodeQueueService.ClearFinishedAsync();
		if (count == 0)
		{
			this.Snackbar.Add("No finished items to clear.", Severity.Info);
			return;
		}

		this.Snackbar.Add($"Cleared {count} finished item(s) from the default view.", Severity.Success);
	}

	private async Task OnShowClearedChanged(bool value)
	{
		this._showCleared = value;
		this._loadGeneration++;
		this._items = [];
		this._skip = 0;
		this._hasMore = true;
		await this.LoadFirstPageAsync();
	}

	private async Task OpenDetailDialogAsync(EncodeQueueItem item)
	{
		var parameters = new DialogParameters<QueueDetailDialog>
		{
			{ x => x.Item, item },
		};
		var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true };
		var dialog = await this.DialogService.ShowAsync<QueueDetailDialog>("Queue Item Details", parameters, options);
		var result = await dialog.Result;
		if (result is { Canceled: false })
		{
			this.StateHasChanged();
		}
	}

	private static int GetTrackCount(string tracksJson)
	{
		try
		{
			return JsonSerializer.Deserialize<List<EncodeTrackConfig>>(tracksJson)?.Count ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private static int FormatPercent(int? value) => Math.Clamp(value ?? 0, 0, 100);

	private static Color GetStatusColor(string status) => status switch
	{
		"Encoding" => Color.Warning,
		"CancelRequested" => Color.Secondary,
		"Canceled" => Color.Secondary,
		"Finished" => Color.Success,
		"Failed" => Color.Error,
		_ => Color.Default,
	};

	private static bool CanResetToQueued(string status) =>
		string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "CancelRequested", StringComparison.OrdinalIgnoreCase);

	private static bool CanCancelEncoding(string status) =>
		string.Equals(status, "Encoding", StringComparison.OrdinalIgnoreCase);

	private static string DiscLabel(string discPath) =>
		Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar, '/'))
		?? discPath;

	public async ValueTask DisposeAsync()
	{
		this.Notifier.ItemUpserted -= this.OnItemUpserted;
		this.Notifier.ItemDeleted -= this.OnItemDeleted;

		if (this._jsObserver is not null)
		{
			try
			{
				await this._jsObserver.InvokeVoidAsync("disconnect");
			}
			catch (JSDisconnectedException) { }
			await this._jsObserver.DisposeAsync();
		}

		if (this._jsModule is not null)
		{
			try
			{
				await this._jsModule.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}

		this._dotNetRef?.Dispose();
	}
}
