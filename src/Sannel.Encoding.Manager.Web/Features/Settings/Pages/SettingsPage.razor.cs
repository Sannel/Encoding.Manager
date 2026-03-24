using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Settings.Dto;
using Sannel.Encoding.Manager.Web.Features.Settings.Entities;
using Sannel.Encoding.Manager.Web.Features.Settings.Services;

namespace Sannel.Encoding.Manager.Web.Features.Settings.Pages;

public partial class SettingsPage : ComponentBase
{
	[Inject]
	private ISettingsService SettingsService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private IOptions<FilesystemOptions> FilesystemOptions { get; set; } = default!;

	private IReadOnlyList<RootDirectory> _roots = [];
	private string? _trackDestinationRoot;
	private string _trackDestinationTemplate = string.Empty;
	private AudioDefault _audioDefault = AudioDefault.Opus;
	private bool _isSaving;
	private bool _isLoading = true;

	protected override async Task OnInitializedAsync()
	{
		this._roots = this.FilesystemOptions.Value.Roots;
		var settings = await this.SettingsService.GetSettingsAsync();
		this._trackDestinationRoot = settings.TrackDestinationRoot;
		this._trackDestinationTemplate = settings.TrackDestinationTemplate;
		this._audioDefault = Enum.TryParse<AudioDefault>(settings.AudioDefault, ignoreCase: true, out var parsed)
			? parsed
			: AudioDefault.Opus;
		this._isLoading = false;
	}

	private void AppendVariable(string variable)
	{
		this._trackDestinationTemplate = this._trackDestinationTemplate + variable;
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			await this.SettingsService.SaveSettingsAsync(new AppSettings
			{
				TrackDestinationTemplate = this._trackDestinationTemplate,
				AudioDefault = this._audioDefault.ToString(),
			});
			this.Snackbar.Add("Settings saved.", Severity.Success);
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to save settings: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isSaving = false;
		}
	}
}
