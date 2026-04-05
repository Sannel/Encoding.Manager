using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Settings.Components;
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
	private IDialogService DialogService { get; set; } = default!;

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private IOptions<FilesystemOptions> FilesystemOptions { get; set; } = default!;

	private IReadOnlyList<RootDirectory> _roots = [];
	private IReadOnlyList<EncodingPreset> _presets = [];
	private IReadOnlyList<LanguageDefinition> _allLanguages = LanguageList.Languages;
	private string? _trackDestinationRoot;
	private string _trackDestinationTemplate = string.Empty;
	private string _movieTrackDestinationTemplate = string.Empty;
	private AudioDefault _audioDefault = AudioDefault.Opus;
	private IReadOnlyCollection<string> _selectedAudioLanguages = new HashSet<string> { "eng" };
	private IReadOnlyCollection<string> _selectedSubtitleLanguages = new HashSet<string> { "eng" };
	private string _tvdbLanguage = "eng";
	private bool _isSaving;
	private bool _isLoading = true;

	protected override async Task OnInitializedAsync()
	{
		this._roots = this.FilesystemOptions.Value.Roots;
		var settings = await this.SettingsService.GetSettingsAsync();
		this._trackDestinationRoot = settings.TrackDestinationRoot;
		this._trackDestinationTemplate = settings.TrackDestinationTemplate;
		this._movieTrackDestinationTemplate = settings.MovieTrackDestinationTemplate;
		this._audioDefault = Enum.TryParse<AudioDefault>(settings.AudioDefault, ignoreCase: true, out var parsed)
			? parsed
			: AudioDefault.Opus;
		this._selectedAudioLanguages = settings.AudioLanguages
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToHashSet();
		this._selectedSubtitleLanguages = settings.SubtitleLanguages
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToHashSet();
		this._tvdbLanguage = settings.TvdbLanguage;
		this._presets = await this.PresetService.GetPresetsAsync();
		this._isLoading = false;
	}

	private void AppendToTvTemplate(string variable)
	{
		this._trackDestinationTemplate = this._trackDestinationTemplate + variable;
	}

	private void AppendToMovieTemplate(string variable)
	{
		this._movieTrackDestinationTemplate = this._movieTrackDestinationTemplate + variable;
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			await this.SettingsService.SaveSettingsAsync(new AppSettings
			{
				TrackDestinationRoot = this._trackDestinationRoot,
				TrackDestinationTemplate = this._trackDestinationTemplate,
				MovieTrackDestinationTemplate = this._movieTrackDestinationTemplate,
				AudioDefault = this._audioDefault.ToString(),
				AudioLanguages = string.Join(",", this._selectedAudioLanguages),
				SubtitleLanguages = string.Join(",", this._selectedSubtitleLanguages),
				TvdbLanguage = this._tvdbLanguage,
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

	private async Task AddPresetAsync()
	{
		var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
		var dialog = await this.DialogService.ShowAsync<AddPresetDialog>("Add Preset", options);
		var result = await dialog.Result;
		if (result is { Canceled: false, Data: EncodingPreset preset })
		{
			await this.PresetService.AddPresetAsync(preset);
			this._presets = await this.PresetService.GetPresetsAsync();
		}
	}

	private async Task DeletePresetAsync(Guid id)
	{
		await this.PresetService.DeletePresetAsync(id);
		this._presets = await this.PresetService.GetPresetsAsync();
		this.Snackbar.Add("Preset deleted.", Severity.Success);
	}
}
