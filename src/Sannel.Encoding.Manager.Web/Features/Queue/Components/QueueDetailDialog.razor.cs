using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Queue.Components;

public partial class QueueDetailDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public required EncodeQueueItem Item { get; set; }

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private IEncodeQueueService EncodeQueueService { get; set; } = default!;

	[Inject]
	private IOptions<FilesystemOptions> FilesystemOptions { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private IReadOnlyList<EncodingPreset> _presets = [];
	private List<EncodeTrackConfig> _tracks = [];
	private List<string> _encodingCommands = [];
	private string _discRootLabel = string.Empty;
	private string _discRelativePath = string.Empty;
	private string? _globalPresetLabel;
	private bool _isSaving;

	protected override async Task OnInitializedAsync()
	{
		this._presets = await this.PresetService.GetPresetsAsync();
		try
		{
			this._tracks = JsonSerializer.Deserialize<List<EncodeTrackConfig>>(this.Item.TracksJson) ?? [];
		}
		catch
		{
			this._tracks = [];
		}

		this.ParseDiscPath(this.Item.DiscPath);

		if (!string.IsNullOrEmpty(this.Item.EncodingCommandsJson))
		{
			try
			{
				this._encodingCommands = JsonSerializer.Deserialize<List<string>>(this.Item.EncodingCommandsJson) ?? [];
			}
			catch
			{
				this._encodingCommands = [];
			}
		}
	}

	private void ParseDiscPath(string discPath)
	{
		foreach (var root in this.FilesystemOptions.Value.Roots)
		{
			var canonicalRoot = Path.GetFullPath(root.Path);
			var canonicalDisc = Path.GetFullPath(discPath);
			if (canonicalDisc.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
			{
				var relative = Path.GetRelativePath(canonicalRoot, canonicalDisc);
				this._discRootLabel = root.Label;
				this._discRelativePath = relative.Replace('\\', '/');
				return;
			}
		}

		this._discRootLabel = string.Empty;
		this._discRelativePath = Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar)) ?? discPath;
	}

	private void ApplyGlobalPreset(string? label)
	{
		this._globalPresetLabel = label;
		foreach (var track in this._tracks)
		{
			track.PresetLabel = label;
		}
	}

	private async Task SaveAsync()
	{
		this._isSaving = true;
		try
		{
			await this.EncodeQueueService.UpdateTracksAsync(this.Item.Id, this._tracks);
			this.Snackbar.Add("Tracks updated.", Severity.Success);
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
