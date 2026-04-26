using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Pages;

public partial class JellyfinPage : ComponentBase
{
	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinSyncService SyncService { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IJellyfinClientFactory ClientFactory { get; set; } = default!;

	[Inject]
	private JellyfinServerService ServerServiceImpl { get; set; } = default!;

	private List<JellyfinServer> _servers = [];
	private List<DestRootViewModel> _destRoots = [];
	private List<JellyfinSyncProfile> _syncProfiles = [];
	private readonly Dictionary<Guid, (int Processed, int Total)> _syncProgress = [];
	private bool _isLoadingServers = true;
	private bool _isLoadingRoots = true;
	private bool _isLoadingProfiles = true;

	protected override async Task OnInitializedAsync()
	{
		await this.LoadServersAsync();
		await this.LoadSyncProfilesAsync();
	}

	private async Task LoadServersAsync()
	{
		this._isLoadingServers = true;
		try
		{
			this._servers = (await this.ServerService.GetAllServersAsync()).ToList();
			await this.LoadDestRootsAsync();
		}
		finally
		{
			this._isLoadingServers = false;
		}
	}

	private async Task LoadDestRootsAsync()
	{
		this._isLoadingRoots = true;
		try
		{
			var roots = new List<DestRootViewModel>();
			foreach (var server in this._servers.Where(s => s.IsDestination))
			{
				var serverRoots = await this.ServerService.GetDestinationRootsAsync(server.Id);
				roots.AddRange(serverRoots.Select(r => new DestRootViewModel(r, server.Name)));
			}
			this._destRoots = roots;
		}
		finally
		{
			this._isLoadingRoots = false;
		}
	}

	private async Task OpenAddServerDialogAsync()
	{
		var dialog = await this.DialogService.ShowAsync<ServerConfigDialog>("Add Server",
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadServersAsync();
		}
	}

	private async Task OpenEditServerDialogAsync(JellyfinServer server)
	{
		var parameters = new DialogParameters<ServerConfigDialog>
		{
			{ x => x.Server, server }
		};
		var dialog = await this.DialogService.ShowAsync<ServerConfigDialog>("Edit Server", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadServersAsync();
		}
	}

	private async Task DeleteServerAsync(Guid serverId)
	{
		var confirmed = await this.DialogService.ShowMessageBoxAsync("Delete Server",
			"Are you sure you want to delete this server? This will also delete associated destination roots.",
			yesText: "Delete", cancelText: "Cancel");
		if (confirmed == true)
		{
			if (await this.ServerService.DeleteServerAsync(serverId))
			{
				this.Snackbar.Add("Server deleted.", Severity.Success);
				await this.LoadServersAsync();
			}
			else
			{
				this.Snackbar.Add("Server not found.", Severity.Warning);
			}
		}
	}

	private async Task PingServerAsync(Guid serverId)
	{
		try
		{
			var server = await this.ServerService.GetServerAsync(serverId);
			if (server is null)
			{
				this.Snackbar.Add("Server not found.", Severity.Warning);
				return;
			}

			var client = this.ClientFactory.CreateClient(
				server.BaseUrl,
				this.ServerServiceImpl.DecryptApiKey(server.ApiKey));
			var info = await client.GetSystemInfoAsync();
			this.Snackbar.Add($"Server is online! {info?.ServerName} v{info?.Version}", Severity.Success);
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Ping error: {ex.Message}", Severity.Error);
		}
	}

	private async Task OpenAddDestRootDialogAsync()
	{
		var parameters = new DialogParameters<DestRootConfigDialog>
		{
			{ x => x.Servers, this._servers.Where(s => s.IsDestination).ToList() }
		};
		var dialog = await this.DialogService.ShowAsync<DestRootConfigDialog>("Add Destination Root", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadDestRootsAsync();
		}
	}

	private async Task OpenEditDestRootDialogAsync(JellyfinDestinationRoot root)
	{
		var parameters = new DialogParameters<DestRootConfigDialog>
		{
			{ x => x.Root, root },
			{ x => x.Servers, this._servers.Where(s => s.IsDestination).ToList() }
		};
		var dialog = await this.DialogService.ShowAsync<DestRootConfigDialog>("Edit Destination Root", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadDestRootsAsync();
		}
	}

	private async Task DeleteDestRootAsync(Guid rootId)
	{
		var confirmed = await this.DialogService.ShowMessageBoxAsync("Delete Root",
			"Are you sure you want to delete this destination root?",
			yesText: "Delete", cancelText: "Cancel");
		if (confirmed == true)
		{
			if (await this.ServerService.DeleteDestinationRootAsync(rootId))
			{
				this.Snackbar.Add("Root deleted.", Severity.Success);
				await this.LoadDestRootsAsync();
			}
		}
	}

	private sealed record DestRootViewModel(JellyfinDestinationRoot Root, string ServerName);

	// --- Sync Profiles ---

	private async Task LoadSyncProfilesAsync()
	{
		this._isLoadingProfiles = true;
		try
		{
			this._syncProfiles = (await this.SyncService.GetAllSyncProfilesAsync()).ToList();
		}
		finally
		{
			this._isLoadingProfiles = false;
		}
	}

	private async Task OpenAddSyncProfileDialogAsync()
	{
		var parameters = new DialogParameters<SyncProfileDialog>
		{
			{ x => x.Servers, this._servers }
		};
		var dialog = await this.DialogService.ShowAsync<SyncProfileDialog>("Add Sync Profile", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadSyncProfilesAsync();
		}
	}

	private async Task OpenEditSyncProfileDialogAsync(JellyfinSyncProfile profile)
	{
		var parameters = new DialogParameters<SyncProfileDialog>
		{
			{ x => x.Profile, profile },
			{ x => x.Servers, this._servers }
		};
		var dialog = await this.DialogService.ShowAsync<SyncProfileDialog>("Edit Sync Profile", parameters,
			new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
		var result = await dialog.Result;
		if (result is not null && !result.Canceled)
		{
			await this.LoadSyncProfilesAsync();
		}
	}

	private async Task DeleteSyncProfileAsync(Guid profileId)
	{
		var confirmed = await this.DialogService.ShowMessageBoxAsync("Delete Sync Profile",
			"Are you sure you want to delete this sync profile?",
			yesText: "Delete", cancelText: "Cancel");
		if (confirmed == true)
		{
			if (await this.SyncService.DeleteSyncProfileAsync(profileId))
			{
				this.Snackbar.Add("Sync profile deleted.", Severity.Success);
				await this.LoadSyncProfilesAsync();
			}
		}
	}

	private async Task OpenSyncDebugDialogAsync(JellyfinSyncProfile profile)
	{
		var parameters = new DialogParameters<SyncDebugDialog>
		{
			{ x => x.Profile, profile }
		};
		await this.DialogService.ShowAsync<SyncDebugDialog>($"Sync Debug: {profile.Name}", parameters,
			new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true });
	}

	private async Task RunSyncNowAsync(Guid profileId)
	{
		var profile = await this.SyncService.GetSyncProfileAsync(profileId);
		if (profile is null)
		{
			this.Snackbar.Add("Sync profile not found.", Severity.Warning);
			return;
		}

		this._syncProgress[profileId] = (0, 0);
		this.StateHasChanged();

		var progress = new Progress<(int Processed, int Total)>(p =>
		{
			this._syncProgress[profileId] = p;
			this.InvokeAsync(this.StateHasChanged);
		});

		try
		{
			await this.SyncService.SyncProfileAsync(profile, progress);
			await this.LoadSyncProfilesAsync();
			this.Snackbar.Add("Sync completed.", Severity.Success);
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._syncProgress.Remove(profileId);
			this.StateHasChanged();
		}
	}

	private static Color GetSyncStatusColor(string? status) =>
		status switch
		{
			"Success" => Color.Success,
			_ when status?.StartsWith("Failed") == true => Color.Error,
			_ => Color.Default,
		};
}
