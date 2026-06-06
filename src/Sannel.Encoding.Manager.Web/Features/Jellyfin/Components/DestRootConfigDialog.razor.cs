using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class DestRootConfigDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public JellyfinDestinationRoot? Root { get; set; }

	[Parameter]
	public List<JellyfinServer> Servers { get; set; } = [];

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinClientFactory ClientFactory { get; set; } = default!;

	[Inject]
	private JellyfinServerService ServerServiceImpl { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private string _name = string.Empty;
	private Guid _serverId;
	private string _rootPath = string.Empty;
	private bool _isSaving;
	private bool _isLoadingLibraries;

	private List<JellyfinVirtualFolder> _libraries = [];
	private string _selectedLibraryName = string.Empty;
	private string[] _selectedLocations = [];

	private bool CanSave =>
		!string.IsNullOrWhiteSpace(this._name) &&
		this._serverId != Guid.Empty &&
		!string.IsNullOrWhiteSpace(this._rootPath);

	protected override async Task OnInitializedAsync()
	{
		if (this.Root is not null)
		{
			this._name = this.Root.Name;
			this._serverId = this.Root.ServerId;
			this._rootPath = this.Root.RootPath;
			await this.LoadLibrariesAsync(this._serverId);
		}
		else if (this.Servers.Count > 0)
		{
			this._serverId = this.Servers[0].Id;
			await this.LoadLibrariesAsync(this._serverId);
		}
	}

	private async Task OnServerChangedAsync(Guid serverId)
	{
		this._serverId = serverId;
		this._selectedLibraryName = string.Empty;
		this._selectedLocations = [];
		this._rootPath = string.Empty;
		await this.LoadLibrariesAsync(serverId);
	}

	private async Task LoadLibrariesAsync(Guid serverId)
	{
		var server = this.Servers.FirstOrDefault(s => s.Id == serverId);
		if (server is null)
		{
			return;
		}

		this._isLoadingLibraries = true;
		try
		{
			var client = this.ClientFactory.CreateClient(
				server.BaseUrl,
				this.ServerServiceImpl.DecryptApiKey(server.ApiKey));
			this._libraries = (await client.GetVirtualFoldersAsync()).ToList();
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to load libraries: {ex.Message}", Severity.Warning);
			this._libraries = [];
		}
		finally
		{
			this._isLoadingLibraries = false;
		}
	}

	private void OnLibraryChanged(string libraryName)
	{
		this._selectedLibraryName = libraryName;
		var lib = this._libraries.FirstOrDefault(l => l.Name == libraryName);
		this._selectedLocations = lib?.Locations ?? [];
		this._rootPath = this._selectedLocations.Length == 1
			? this._selectedLocations[0]
			: string.Empty;

		if (string.IsNullOrWhiteSpace(this._name))
		{
			this._name = libraryName;
		}
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			var dto = new JellyfinDestinationRootDto
			{
				Name = this._name,
				ServerId = this._serverId,
				RootPath = this._rootPath.TrimEnd('/'),
			};

			if (this.Root is null)
			{
				await this.ServerService.CreateDestinationRootAsync(dto);
				this.Snackbar.Add("Root added.", Severity.Success);
			}
			else
			{
				await this.ServerService.UpdateDestinationRootAsync(this.Root.Id, dto);
				this.Snackbar.Add("Root updated.", Severity.Success);
			}

			this.MudDialog.Close(DialogResult.Ok(true));
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to save: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isSaving = false;
		}
	}

	private void Cancel() => this.MudDialog.Cancel();
}
