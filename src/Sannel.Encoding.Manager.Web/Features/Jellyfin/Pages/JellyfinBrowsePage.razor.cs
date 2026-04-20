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

	private List<JellyfinVirtualFolder> _libraries = [];
	private string? _selectedLibraryId;

	private List<BreadcrumbItem> _breadcrumbs = [];
	private readonly List<(string Id, string Name, string Type)> _navigationStack = [];

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

		try
		{
			this._libraries = (await this._client.GetVirtualFoldersAsync()).ToList();
		}
		catch
		{
			this._libraries = [];
		}

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
		this._parentId = this._selectedLibraryId;
		this._navigationStack.Clear();
		this.UpdateBreadcrumbs();
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
				Recursive = this._parentId is null,
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

	private async Task DrillIntoSeriesAsync(JellyfinItem series)
	{
		this._navigationStack.Add((series.Id, series.Name, "Series"));
		this._parentId = series.Id;
		this._selectedType = "Season";
		this._search = string.Empty;
		this._page = 0;
		this.UpdateBreadcrumbs();
		await this.LoadPageAsync();
	}

	private async Task DrillIntoSeasonAsync(JellyfinItem season)
	{
		this._navigationStack.Add((season.Id, season.Name, "Season"));
		this._parentId = season.Id;
		this._selectedType = "Episode";
		this._search = string.Empty;
		this._page = 0;
		this.UpdateBreadcrumbs();
		await this.LoadPageAsync();
	}

	private async Task NavigateToBreadcrumbAsync(int index)
	{
		if (index < 0)
		{
			this._parentId = null;
			this._selectedType = "Movie";
			this._search = string.Empty;
			this._navigationStack.Clear();
		}
		else
		{
			var entry = this._navigationStack[index];
			this._parentId = entry.Id;
			this._selectedType = entry.Type switch
			{
				"Series" => "Season",
				"Season" => "Episode",
				_ => "Episode",
			};
			this._search = string.Empty;
			this._navigationStack.RemoveRange(index + 1, this._navigationStack.Count - index - 1);
		}

		this._page = 0;
		this.UpdateBreadcrumbs();
		await this.LoadPageAsync();
	}

	private void UpdateBreadcrumbs()
	{
		this._breadcrumbs = [];

		if (this._navigationStack.Count > 0)
		{
			this._breadcrumbs.Add(new BreadcrumbItem("Library", null, false, Icons.Material.Filled.Home));
		}

		for (var i = 0; i < this._navigationStack.Count; i++)
		{
			var entry = this._navigationStack[i];
			var isLast = i == this._navigationStack.Count - 1;
			this._breadcrumbs.Add(new BreadcrumbItem(entry.Name, null, isLast));
		}
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

	private async Task OpenBulkQueueDialogAsync(JellyfinItem parentItem)
	{
		if (this._client is null)
		{
			return;
		}

		var title = string.Equals(parentItem.Type, "Series", StringComparison.OrdinalIgnoreCase)
			? $"Queue Series: {parentItem.Name}"
			: $"Queue Season: {parentItem.Name}";

		var parameters = new DialogParameters<QueueBulkDialog>
		{
			{ x => x.ServerId, this.ServerId },
			{ x => x.Client, this._client },
			{ x => x.ParentItem, parentItem },
		};
		var dialog = await this.DialogService.ShowAsync<QueueBulkDialog>(title, parameters,
			new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
		await dialog.Result;
	}
}
