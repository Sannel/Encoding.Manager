using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class QueueJellyfinItemDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public Guid ServerId { get; set; }

	[Parameter]
	public required JellyfinItem Item { get; set; }

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinEncodeService EncodeService { get; set; } = default!;

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private List<JellyfinServer> _destServers = [];
	private List<JellyfinDestinationRoot> _destRoots = [];
	private IReadOnlyList<EncodingPreset> _presets = [];
	private Guid _destServerId;
	private Guid _destRootId;
	private string _presetLabel = string.Empty;
	private bool _isQueueing;

	private bool CanQueue =>
		this._destServerId != Guid.Empty &&
		this._destRootId != Guid.Empty &&
		!string.IsNullOrWhiteSpace(this._presetLabel);

	protected override async Task OnInitializedAsync()
	{
		var servers = await this.ServerService.GetAllServersAsync();
		this._destServers = servers.Where(s => s.IsDestination).ToList();
		this._presets = await this.PresetService.GetPresetsAsync();

		if (this._destServers.Count > 0)
		{
			this._destServerId = this._destServers[0].Id;
			await this.LoadRootsAsync();
		}
	}

	private async Task LoadRootsAsync()
	{
		this._destRoots = (await this.ServerService.GetDestinationRootsAsync(this._destServerId)).ToList();
		if (this._destRoots.Count > 0)
		{
			this._destRootId = this._destRoots[0].Id;
		}
	}

	private async Task QueueAsync()
	{
		this._isQueueing = true;
		try
		{
			var request = new JellyfinEncodeRequest
			{
				ServerId = this.ServerId,
				ItemId = this.Item.Id,
				PresetLabel = this._presetLabel,
				DestServerId = this._destServerId,
				DestRootId = this._destRootId,
			};

			await this.EncodeService.QueueItemAsync(request);
			this.MudDialog.Close(DialogResult.Ok(true));
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to queue: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isQueueing = false;
		}
	}

	private void Cancel() => this.MudDialog.Cancel();
}
