using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Pages;

/// <summary>
/// Code-behind for the Filesystem browser page.
/// </summary>
public partial class FilesystemPage : ComponentBase
{
	[Inject]
	private IFilesystemService FilesystemService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Inject]
	private NavigationManager NavigationManager { get; set; } = default!;

	private List<ConfiguredDirectoryResponse> _configuredRoots = [];
	private string? _selectedRootLabel;
	private BrowseResponse? _currentBrowse;
	private string? _currentRelativePath;
	private string? _currentDisplayPath;
	private bool _isLoading = true;
	private string? _errorMessage;
	private string? _selectedItem;
	private string? _selectedItemType;

	protected override async Task OnInitializedAsync()
	{
		this._isLoading = true;
		try
		{
			var roots = await this.FilesystemService.GetConfiguredDirectoriesAsync();
			this._configuredRoots = roots.ToList();
		}
		catch (Exception ex)
		{
			this._errorMessage = $"Error loading configured roots: {ex.Message}";
			this.Snackbar.Add(this._errorMessage, Severity.Error);
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private async Task OnRootSelected(string? label)
	{
		this._selectedRootLabel = label;
		this._currentRelativePath = null;
		this._currentDisplayPath = label;
		this._currentBrowse = null;
		if (!string.IsNullOrEmpty(label))
		{
			await this.LoadBrowseData();
		}
	}

	private async Task OpenFolder(string folderName)
	{
		if (string.IsNullOrEmpty(this._selectedRootLabel))
		{
			return;
		}

		var newPath = string.IsNullOrEmpty(this._currentRelativePath)
			? folderName
			: Path.Combine(this._currentRelativePath, folderName).Replace("\\", "/");

		this._currentRelativePath = newPath;
		this._currentDisplayPath = $"{this._selectedRootLabel}/{newPath}";
		await this.LoadBrowseData();
	}

	private async Task UpFolder()
	{
		if (string.IsNullOrEmpty(this._currentRelativePath))
		{
			return;
		}

		var lastSlash = this._currentRelativePath.LastIndexOf('/');
		this._currentRelativePath = lastSlash > 0
			? this._currentRelativePath[..lastSlash]
			: null;

		this._currentDisplayPath = string.IsNullOrEmpty(this._currentRelativePath)
			? this._selectedRootLabel
			: $"{this._selectedRootLabel}/{this._currentRelativePath}";

		await this.LoadBrowseData();
	}

	private void SelectFile(FileEntryResponse file)
	{
		var relativePath = string.IsNullOrEmpty(this._currentRelativePath)
			? file.Name
			: $"{this._currentRelativePath}/{file.Name}";

		this._selectedItem = $"{this._selectedRootLabel}:{relativePath}";
		this._selectedItemType = "File";
		this.Snackbar.Add($"Selected file: {file.Name}", Severity.Success);
	}

	private void SelectDiscFolder(DirectoryEntryResponse folder)
	{
		var relativePath = string.IsNullOrEmpty(this._currentRelativePath)
			? folder.Name
			: $"{this._currentRelativePath}/{folder.Name}";

		var root = Uri.EscapeDataString(this._selectedRootLabel!);
		var path = Uri.EscapeDataString(relativePath);
		var discType = Uri.EscapeDataString(folder.DiscType.ToString());
		this.NavigationManager.NavigateTo($"/scan?root={root}&path={path}&discType={discType}");
	}

	private void ClearSelection()
	{
		this._selectedItem = null;
		this._selectedItemType = null;
	}

	private async Task LoadBrowseData()
	{
		if (string.IsNullOrEmpty(this._selectedRootLabel))
		{
			return;
		}

		this._isLoading = true;
		this._errorMessage = null;
		try
		{
			this._currentBrowse = await this.FilesystemService.BrowseAsync(this._selectedRootLabel, this._currentRelativePath);
		}
		catch (ArgumentException ex)
		{
			this._errorMessage = $"Invalid request: {ex.Message}";
			this.Snackbar.Add(this._errorMessage, Severity.Error);
			this._currentBrowse = null;
		}
		catch (DirectoryNotFoundException ex)
		{
			this._errorMessage = $"Directory not found: {ex.Message}";
			this.Snackbar.Add(this._errorMessage, Severity.Error);
			this._currentBrowse = null;
		}
		catch (Exception ex)
		{
			this._errorMessage = $"Error browsing directory: {ex.Message}";
			this.Snackbar.Add(this._errorMessage, Severity.Error);
			this._currentBrowse = null;
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private List<object> GetTableItems()
	{
		if (this._currentBrowse == null)
		{
			return [];
		}

		var items = new List<object>();
		items.AddRange(this._currentBrowse.Directories);
		items.AddRange(this._currentBrowse.Files);
		return items;
	}

	private string FormatBytes(long bytes)
	{
		var units = new[] { "B", "KB", "MB", "GB", "TB" };
		var size = (double)bytes;
		var unitIndex = 0;

		while (size >= 1024 && unitIndex < units.Length - 1)
		{
			size /= 1024;
			unitIndex++;
		}

		return $"{size:F2} {units[unitIndex]}";
	}
}
