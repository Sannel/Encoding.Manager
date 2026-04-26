using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class SyncProfileDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public JellyfinSyncProfile? Profile { get; set; }

	[Parameter]
	public List<JellyfinServer> Servers { get; set; } = [];

	[Inject]
	private IJellyfinSyncService SyncService { get; set; } = default!;

	[Inject]
	private IJellyfinClientFactory ClientFactory { get; set; } = default!;

	[Inject]
	private JellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private string _name = string.Empty;
	private Guid _serverAId;
	private string _userIdA = string.Empty;
	private Guid _serverBId;
	private string _userIdB = string.Empty;
	private int _syncIntervalMinutes = 60;
	private bool _isEnabled = true;
	private bool _isSaving;

	private List<JellyfinUser> _usersA = [];
	private List<JellyfinUser> _usersB = [];

	private bool CanSave =>
		!string.IsNullOrWhiteSpace(this._name) &&
		this._serverAId != Guid.Empty &&
		!string.IsNullOrWhiteSpace(this._userIdA) &&
		this._serverBId != Guid.Empty &&
		!string.IsNullOrWhiteSpace(this._userIdB);

	protected override async Task OnInitializedAsync()
	{
		if (this.Profile is not null)
		{
			this._name = this.Profile.Name;
			this._serverAId = this.Profile.ServerAId;
			this._userIdA = this.Profile.UserIdA;
			this._serverBId = this.Profile.ServerBId;
			this._userIdB = this.Profile.UserIdB;
			this._syncIntervalMinutes = this.Profile.SyncIntervalMinutes;
			this._isEnabled = this.Profile.IsEnabled;

			await this.LoadUsersForServerAsync(this._serverAId, isServerA: true);
			await this.LoadUsersForServerAsync(this._serverBId, isServerA: false);
		}
		else if (this.Servers.Count >= 2)
		{
			this._serverAId = this.Servers[0].Id;
			this._serverBId = this.Servers[1].Id;
			await this.LoadUsersForServerAsync(this._serverAId, isServerA: true);
			await this.LoadUsersForServerAsync(this._serverBId, isServerA: false);
		}
		else if (this.Servers.Count == 1)
		{
			this._serverAId = this.Servers[0].Id;
			await this.LoadUsersForServerAsync(this._serverAId, isServerA: true);
		}
	}

	private async Task OnServerAChanged(Guid serverId)
	{
		this._serverAId = serverId;
		this._userIdA = string.Empty;
		await this.LoadUsersForServerAsync(serverId, isServerA: true);
	}

	private async Task OnServerBChanged(Guid serverId)
	{
		this._serverBId = serverId;
		this._userIdB = string.Empty;
		await this.LoadUsersForServerAsync(serverId, isServerA: false);
	}

	private async Task LoadUsersForServerAsync(Guid serverId, bool isServerA)
	{
		var server = this.Servers.FirstOrDefault(s => s.Id == serverId);
		if (server is null)
		{
			return;
		}

		try
		{
			var client = this.ClientFactory.CreateClient(
				server.BaseUrl,
				this.ServerService.DecryptApiKey(server.ApiKey));
			var users = await client.GetUsersAsync();

			if (isServerA)
			{
				this._usersA = users.ToList();
			}
			else
			{
				this._usersB = users.ToList();
			}
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to load users: {ex.Message}", Severity.Warning);
			if (isServerA)
			{
				this._usersA = [];
			}
			else
			{
				this._usersB = [];
			}
		}
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			var dto = new JellyfinSyncProfileDto
			{
				Name = this._name,
				ServerAId = this._serverAId,
				UserIdA = this._userIdA,
				ServerBId = this._serverBId,
				UserIdB = this._userIdB,
				IsEnabled = this._isEnabled,
				SyncIntervalMinutes = this._syncIntervalMinutes,
			};

			if (this.Profile is null)
			{
				await this.SyncService.CreateSyncProfileAsync(dto);
				this.Snackbar.Add("Sync profile created.", Severity.Success);
			}
			else
			{
				await this.SyncService.UpdateSyncProfileAsync(this.Profile.Id, dto);
				this.Snackbar.Add("Sync profile updated.", Severity.Success);
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
