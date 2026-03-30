using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Queue.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Pages;

public partial class QueuePage : ComponentBase
{
	[Inject]
	private IEncodeQueueService EncodeQueueService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	private IReadOnlyList<EncodeQueueItem> _items = [];
	private bool _isLoading = true;

	protected override async Task OnInitializedAsync() => await this.LoadAsync();

	private async Task LoadAsync()
	{
		this._isLoading = true;
		this._items = await this.EncodeQueueService.GetItemsAsync();
		this._isLoading = false;
	}

	private async Task DeleteItemAsync(Guid id)
	{
		await this.EncodeQueueService.DeleteItemAsync(id);
		this.Snackbar.Add("Item removed from queue.", Severity.Success);
		await this.LoadAsync();
	}

	private async Task RetryFailedItemAsync(Guid id)
	{
		var reset = await this.EncodeQueueService.ResetToQueuedAsync(id);
		if (reset)
		{
			this.Snackbar.Add("Failed item reset to queued.", Severity.Success);
		}
		else
		{
			this.Snackbar.Add("Only failed items can be reset to queued.", Severity.Warning);
		}

		await this.LoadAsync();
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
			await this.LoadAsync();
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

	private static string DiscLabel(string discPath) =>
		Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar, '/'))
		?? discPath;
}
