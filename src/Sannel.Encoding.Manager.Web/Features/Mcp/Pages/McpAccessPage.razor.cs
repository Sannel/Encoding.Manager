using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Mcp.Components;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Pages;

/// <summary>Lets the signed-in user manage their MCP API key and shows client setup instructions.</summary>
public partial class McpAccessPage : ComponentBase
{
	[Inject]
	private IUserApiKeyService KeyService { get; set; } = default!;

	[Inject]
	private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

	[Inject]
	private NavigationManager NavigationManager { get; set; } = default!;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Inject]
	private IOptions<McpOptions> McpOptions { get; set; } = default!;

	private bool _mcpEnabled;
	private bool _isLoading = true;
	private bool _isBusy;
	private ClaimsPrincipal? _user;
	private string? _objectId;
	private UserApiKeyInfo? _keyInfo;

	/// <summary>The plaintext key, held only in this circuit's memory right after generate/rotate.</summary>
	private string? _revealedKey;

	private string EndpointUrl => this.NavigationManager.BaseUri.TrimEnd('/') + "/mcp";

	protected override async Task OnInitializedAsync()
	{
		this._mcpEnabled = this.McpOptions.Value.Enabled;
		var state = await this.AuthenticationStateProvider.GetAuthenticationStateAsync();
		this._user = state.User;
		this._objectId = UserIdentity.GetObjectId(this._user);
		if (this._objectId is not null)
		{
			this._keyInfo = await this.KeyService.GetForUserAsync(this._objectId);
		}

		this._isLoading = false;
	}

	private async Task GenerateAsync() => await this.IssueKeyAsync("API key generated.");

	private async Task RotateAsync()
	{
		var dialog = await this.DialogService.ShowAsync<RotateKeyConfirmDialog>("Rotate API key");
		var result = await dialog.Result;
		if (result is null || result.Canceled)
		{
			return;
		}

		await this.IssueKeyAsync("API key rotated. Update your AI clients with the new key.");
	}

	private async Task RevokeAsync()
	{
		var parameters = new DialogParameters<RotateKeyConfirmDialog>
		{
			{ d => d.Title, "Revoke API key?" },
			{ d => d.Message, "The key will be deleted. You can generate a new one later." },
			{ d => d.ConfirmText, "Revoke" },
			{ d => d.ConfirmColor, Color.Error },
		};
		var dialog = await this.DialogService.ShowAsync<RotateKeyConfirmDialog>("Revoke API key", parameters);
		var result = await dialog.Result;
		if (result is null || result.Canceled || this._objectId is null)
		{
			return;
		}

		this._isBusy = true;
		try
		{
			await this.KeyService.RevokeAsync(this._objectId);
			this._keyInfo = null;
			this._revealedKey = null;
			this.Snackbar.Add("API key revoked.", Severity.Success);
		}
		finally
		{
			this._isBusy = false;
		}
	}

	private async Task IssueKeyAsync(string successMessage)
	{
		if (this._user is null || this._objectId is null)
		{
			this.Snackbar.Add("Your account has no object id, so a key cannot be issued.", Severity.Error);
			return;
		}

		this._isBusy = true;
		try
		{
			var generated = await this.KeyService.CreateOrRotateAsync(this._user);
			this._revealedKey = generated.Key;
			this._keyInfo = generated.Info;
			this.Snackbar.Add(successMessage, Severity.Success);
		}
		finally
		{
			this._isBusy = false;
		}
	}

	private async Task CopyEndpointAsync()
	{
		await this.JS.InvokeVoidAsync("navigator.clipboard.writeText", this.EndpointUrl);
		this.Snackbar.Add("Endpoint URL copied.", Severity.Success);
	}
}
