using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class MetadataPairDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public JellyfinMetadataServerPair? Pair { get; set; }

	[Parameter]
	public List<JellyfinServer> Servers { get; set; } = [];

	[Inject]
	private IJellyfinMetadataSyncService MetadataSyncService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private Guid _sourceServerId;
	private Guid _destinationServerId;
	private bool _isEnabled = true;
	private bool _isSaving;

	private List<JellyfinServer> SourceServers => this.Servers.Where(s => s.IsSource).ToList();
	private List<JellyfinServer> DestinationServers => this.Servers.Where(s => s.IsDestination).ToList();

	private bool CanSave =>
		this._sourceServerId != Guid.Empty &&
		this._destinationServerId != Guid.Empty &&
		this._sourceServerId != this._destinationServerId;

	protected override void OnParametersSet()
	{
		if (this.Pair is not null)
		{
			this._sourceServerId = this.Pair.SourceServerId;
			this._destinationServerId = this.Pair.DestinationServerId;
			this._isEnabled = this.Pair.IsEnabled;
		}
		else
		{
			var firstSource = this.SourceServers.FirstOrDefault();
			var firstDest = this.DestinationServers.FirstOrDefault();
			this._sourceServerId = firstSource?.Id ?? Guid.Empty;
			this._destinationServerId = firstDest?.Id ?? Guid.Empty;
		}
	}

	private async Task SaveAsync()
	{
		if (!this.CanSave)
		{
			return;
		}

		this._isSaving = true;
		try
		{
			var dto = new JellyfinMetadataServerPairDto
			{
				SourceServerId = this._sourceServerId,
				DestinationServerId = this._destinationServerId,
				IsEnabled = this._isEnabled,
			};

			if (this.Pair is null)
			{
				await this.MetadataSyncService.CreatePairAsync(dto).ConfigureAwait(false);
				this.Snackbar.Add("Metadata sync pair created.", Severity.Success);
			}
			else
			{
				await this.MetadataSyncService.UpdatePairAsync(this.Pair.Id, dto).ConfigureAwait(false);
				this.Snackbar.Add("Metadata sync pair updated.", Severity.Success);
			}

			this.MudDialog.Close(DialogResult.Ok(true));
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Error saving pair: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isSaving = false;
		}
	}

	private void Cancel() => this.MudDialog.Cancel();
}
