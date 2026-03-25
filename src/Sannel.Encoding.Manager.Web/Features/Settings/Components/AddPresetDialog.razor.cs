using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Settings.Components;

public partial class AddPresetDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Inject]
	private IFilesystemService FilesystemService { get; set; } = default!;

	private static readonly string[] JsonExtensions = [".json"];

	private string _label = string.Empty;
	private string? _selectedRoot;
	private string? _selectedFileName;
	private string? _browseError;
	private bool _isBrowseLoading;

	private List<DirectoryEntryResponse> _directories = [];
	private List<FileEntryResponse> _files = [];
	private List<string> _pathParts = [];
	private IReadOnlyList<ConfiguredDirectoryResponse> _roots = [];

	private bool CanConfirm =>
		!string.IsNullOrWhiteSpace(this._label)
		&& this._selectedRoot is not null
		&& this._selectedFileName is not null;

	private string CurrentRelativePath =>
		this._pathParts.Count == 0
			? string.Empty
			: string.Join("/", this._pathParts);

	protected override async Task OnInitializedAsync()
	{
		var dirs = await this.FilesystemService.GetConfiguredDirectoriesAsync();
		this._roots = dirs.ToList();
	}

	private async Task NavToAsync(string? relativePath)
	{
		if (this._selectedRoot is null)
		{
			return;
		}

		this._pathParts = relativePath is null
			? []
			: relativePath.Split('/').Where(p => p.Length > 0).ToList();
		this._selectedFileName = null;
		await this.BrowseAsync();
	}

	private async Task NavToPartAsync(int index)
	{
		this._pathParts = this._pathParts.Take(index + 1).ToList();
		this._selectedFileName = null;
		await this.BrowseAsync();
	}

	private async Task NavIntoAsync(string dirName)
	{
		this._pathParts.Add(dirName);
		this._selectedFileName = null;
		await this.BrowseAsync();
	}

	private void SelectFile(string fileName)
	{
		this._selectedFileName = fileName;
		if (string.IsNullOrWhiteSpace(this._label))
		{
			this._label = Path.GetFileNameWithoutExtension(fileName);
		}
	}

	private async Task BrowseAsync()
	{
		if (this._selectedRoot is null)
		{
			return;
		}

		this._isBrowseLoading = true;
		this._browseError = null;
		try
		{
			var result = await this.FilesystemService.BrowseWithExtensionFilterAsync(
				this._selectedRoot,
				this.CurrentRelativePath.Length > 0 ? this.CurrentRelativePath : null,
				JsonExtensions);
			this._directories = result.Directories;
			this._files = result.Files;
		}
		catch (Exception ex)
		{
			this._browseError = ex.Message;
			this._directories = [];
			this._files = [];
		}
		finally
		{
			this._isBrowseLoading = false;
		}
	}

	private async Task OnSelectedRootChangedAsync(string? root)
	{
		this._selectedRoot = root;
		this._pathParts = [];
		this._selectedFileName = null;
		await this.BrowseAsync();
	}

	private void Cancel() => this.MudDialog.Cancel();

	private void ConfirmAsync()
	{
		var relativeParts = new List<string>(this._pathParts) { this._selectedFileName! };
		var preset = new EncodingPreset
		{
			Label = this._label.Trim(),
			RootLabel = this._selectedRoot!,
			RelativePath = string.Join("/", relativeParts),
		};
		this.MudDialog.Close(DialogResult.Ok(preset));
	}
}
