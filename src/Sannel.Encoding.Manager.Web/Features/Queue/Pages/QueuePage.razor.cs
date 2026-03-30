using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Queue.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
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
	private NavigationManager Navigation { get; set; } = default!;

	private IReadOnlyList<EncodeQueueItem> _items = [];
	private bool _isLoading = true;
	private readonly SemaphoreSlim _loadLock = new(1, 1);
	private HubConnection? _hubConnection;

	protected override async Task OnInitializedAsync()
	{
		await this.LoadAsync(showLoadingIndicator: true);
		await this.InitializeSignalRAsync();
	}

	private async Task LoadAsync(bool showLoadingIndicator = false)
	{
		await this._loadLock.WaitAsync();
		try
		{
			if (showLoadingIndicator)
			{
				this._isLoading = true;
			}

			this._items = await this.EncodeQueueService.GetItemsAsync();

			if (showLoadingIndicator)
			{
				this._isLoading = false;
			}
		}
		finally
		{
			this._loadLock.Release();
		}
	}

	private async Task InitializeSignalRAsync()
	{
		this._hubConnection = new HubConnectionBuilder()
			.WithUrl(this.Navigation.ToAbsoluteUri("/hubs/queue"))
			.WithAutomaticReconnect()
			.Build();

		this._hubConnection.On<EncodeQueueItem>("QueueItemUpserted", async item =>
		{
			await this.InvokeAsync(async () =>
			{
				await this.ApplyItemUpsertAsync(item);
				this.StateHasChanged();
			});
		});

		this._hubConnection.On<Guid>("QueueItemDeleted", async id =>
		{
			await this.InvokeAsync(() =>
			{
				this.ApplyItemDeleted(id);
				this.StateHasChanged();
				return Task.CompletedTask;
			});
		});

		this._hubConnection.Reconnected += async _ =>
		{
			await this.InvokeAsync(async () =>
			{
				await this.LoadAsync();
				this.StateHasChanged();
			});
		};

		try
		{
			await this._hubConnection.StartAsync();
		}
		catch
		{
			// Non-fatal: page still works with manual actions if hub connection fails.
		}
	}

	private Task ApplyItemUpsertAsync(EncodeQueueItem item)
	{
		if (item.Id == Guid.Empty)
		{
			return Task.CompletedTask;
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

		return Task.CompletedTask;
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

	private static Color GetStatusColor(string status) => status switch
	{
		"Encoding" => Color.Warning,
		"Finished" => Color.Success,
		"Failed" => Color.Error,
		_ => Color.Default,
	};

	private static bool CanResetToQueued(string status) =>
		string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "Finished", StringComparison.OrdinalIgnoreCase);

	private static string DiscLabel(string discPath) =>
		Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar, '/'))
		?? discPath;

	public async ValueTask DisposeAsync()
	{
		if (this._hubConnection is not null)
		{
			try
			{
				await this._hubConnection.StopAsync();
			}
			catch
			{
				// Ignore connection shutdown errors during disposal.
			}

			await this._hubConnection.DisposeAsync();
		}

		this._loadLock.Dispose();
	}
}
