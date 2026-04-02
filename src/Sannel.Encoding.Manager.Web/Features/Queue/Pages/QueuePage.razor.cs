using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Queue.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Pages;

public partial class QueuePage : ComponentBase, IDisposable
{
	[Inject]
	private IEncodeQueueService EncodeQueueService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private QueueChangeNotifier Notifier { get; set; } = default!;

	private IReadOnlyList<EncodeQueueItem> _items = [];
	private bool _isLoading = true;
	private bool _showCleared;

	protected override async Task OnInitializedAsync()
	{
		await this.LoadAsync(showLoadingIndicator: true);
		this.Notifier.ItemUpserted += this.OnItemUpserted;
		this.Notifier.ItemDeleted += this.OnItemDeleted;
	}

	private async Task LoadAsync(bool showLoadingIndicator = false)
	{
		if (showLoadingIndicator)
		{
			this._isLoading = true;
		}

		this._items = await this.EncodeQueueService.GetItemsAsync(this._showCleared);

		if (showLoadingIndicator)
		{
			this._isLoading = false;
		}
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
		});
	}

	private async void OnItemDeleted(Guid id)
	{
		await this.InvokeAsync(() =>
		{
			this.ApplyItemDeleted(id);
			this.StateHasChanged();
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
			.OrderBy(i => i.CreatedAt)
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
			this.Snackbar.Add("Only failed or finished items can be reset to queued.", Severity.Warning);
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
		await this.LoadAsync(showLoadingIndicator: true);
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
		|| string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase);

	private static bool CanCancelEncoding(string status) =>
		string.Equals(status, "Encoding", StringComparison.OrdinalIgnoreCase);

	private static string DiscLabel(string discPath) =>
		Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar, '/'))
		?? discPath;

	public void Dispose()
	{
		this.Notifier.ItemUpserted -= this.OnItemUpserted;
		this.Notifier.ItemDeleted -= this.OnItemDeleted;
	}
}
