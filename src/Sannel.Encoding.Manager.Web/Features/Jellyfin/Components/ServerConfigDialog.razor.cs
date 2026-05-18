using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class ServerConfigDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public JellyfinServer? Server { get; set; }

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private string _name = string.Empty;
	private string _baseUrl = string.Empty;
	private string _apiKey = string.Empty;
	private bool _isSource = true;
	private bool _isDestination;
	private string? _sftpHost;
	private int _sftpPort = 22;
	private string? _sftpUsername;
	private string? _sftpPassword;
	private bool _isSaving;

	private bool CanSave =>
		!string.IsNullOrWhiteSpace(this._name) &&
		!string.IsNullOrWhiteSpace(this._baseUrl) &&
		!string.IsNullOrWhiteSpace(this._apiKey);

	protected override void OnInitialized()
	{
		if (this.Server is not null)
		{
			this._name = this.Server.Name;
			this._baseUrl = this.Server.BaseUrl;
			this._apiKey = string.Empty; // Don't pre-fill encrypted key
			this._isSource = this.Server.IsSource;
			this._isDestination = this.Server.IsDestination;
			this._sftpHost = this.Server.SftpHost;
			this._sftpPort = this.Server.SftpPort;
			this._sftpUsername = this.Server.SftpUsername;
			this._sftpPassword = string.Empty; // Don't pre-fill encrypted password
		}
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			var dto = new JellyfinServerDto
			{
				Name = this._name,
				BaseUrl = this._baseUrl.TrimEnd('/'),
				ApiKey = this._apiKey,
				IsSource = this._isSource,
				IsDestination = this._isDestination,
				SftpHost = this._sftpHost,
				SftpPort = this._sftpPort,
				SftpUsername = this._sftpUsername,
				SftpPassword = this._sftpPassword,
			};

			if (this.Server is null)
			{
				await this.ServerService.CreateServerAsync(dto);
				this.Snackbar.Add("Server added.", Severity.Success);
			}
			else
			{
				await this.ServerService.UpdateServerAsync(this.Server.Id, dto);
				this.Snackbar.Add("Server updated.", Severity.Success);
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
