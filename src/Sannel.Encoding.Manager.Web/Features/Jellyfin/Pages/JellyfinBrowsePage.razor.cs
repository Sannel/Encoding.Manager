using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Pages;

public partial class JellyfinBrowsePage : ComponentBase
{
	[Parameter]
	public Guid ServerId { get; set; }

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinClientFactory ClientFactory { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private NavigationManager NavigationManager { get; set; } = default!;

	private const int PageSize = 50;

	private string _serverName = string.Empty;
	private JellyfinServer? _server;
	private IJellyfinClient? _client;

	private string _selectedType = "Movie";
	private string _search = string.Empty;
	private string? _parentId;
	private int _page;
	private int _totalCount;
	private bool _isLoading;
	private List<JellyfinItem> _items = [];

	protected override async Task OnInitializedAsync()
	{
		this._server = await this.ServerService.GetServerAsync(this.ServerId);
		if (this._server is null)
		{
			this.NavigationManager.NavigateTo("/jellyfin");
			return;
		}

		this._serverName = this._server.Name;
		var serverServiceImpl = (JellyfinServerService)this.ServerService;
		this._client = this.ClientFactory.CreateClient(this._server.BaseUrl, serverServiceImpl.DecryptApiKey(this._server.ApiKey));

		await this.SearchAsync();
	}

	private async Task SearchAsync()
	{
		if (this._client is null || this._server is null)
		{
			return;
		}

		this._isLoading = true;
		this._page = 0;
		try
		{
			await this.LoadPageAsync();
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private async Task LoadPageAsync()
	{
		if (this._client is null || this._server is null)
		{
			return;
		}

		this._isLoading = true;
		try
		{
			var response = await this._client.GetItemsAsync(new GetItemsRequest
			{
				IncludeItemTypes = this._selectedType,
				SearchTerm = string.IsNullOrWhiteSpace(this._search) ? null : this._search,
				ParentId = this._parentId,
				Recursive = true,
				StartIndex = this._page * PageSize,
				Limit = PageSize,
				Fields = "ProviderIds",
			});

			this._items = response.Items.ToList();
			this._totalCount = response.TotalRecordCount;
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Error loading items: {ex.Message}", Severity.Error);
			this._items = [];
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private async Task DrillIntoAsync(string parentId)
	{
		this._parentId = parentId;
		this._selectedType = "Episode";
		this._page = 0;
		await this.LoadPageAsync();
	}

	private async Task PreviousPageAsync()
	{
		if (this._page > 0)
		{
			this._page--;
			await this.LoadPageAsync();
		}
	}

	private async Task NextPageAsync()
	{
		this._page++;
		await this.LoadPageAsync();
	}

	private async Task OnSearchKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			await this.SearchAsync();
		}
	}

	private async Task OpenQueueDialogAsync(JellyfinItem item)
	{
		var parameters = new DialogParameters<QueueJellyfinItemDialog>
		{
			{ x => x.ServerId, this.ServerId },
			{ x => x.Item, item }
		};
		var dialog = await this.DialogService.ShowAsync<QueueJellyfinItemDialog>("Queue for Encoding", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			this.Snackbar.Add("Item queued for encoding.", Severity.Success);
		}
	}
}
