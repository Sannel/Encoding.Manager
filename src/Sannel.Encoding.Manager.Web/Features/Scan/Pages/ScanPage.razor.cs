using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Pages;

public partial class ScanPage : ComponentBase
{
	[Inject]
	private IFilesystemService FilesystemService { get; set; } = default!;

	[Inject]
	private IHandBrakeService HandBrakeService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[SupplyParameterFromQuery(Name = "root")]
	private string? Root { get; set; }

	[SupplyParameterFromQuery(Name = "path")]
	private string? Path { get; set; }

	[SupplyParameterFromQuery(Name = "discType")]
	private string? DiscType { get; set; }

	[SupplyParameterFromQuery(Name = "selectionType")]
	private string? SelectionType { get; set; }

	private bool _isScanning;
	private string? _errorMessage;
	private HandBrakeScanResult? _scanResult;
	private IReadOnlyList<FileEntryResponse> _folderFiles = [];
	private ScanMode _selectedMode = ScanMode.Titles;

	private bool IsFolderSelection =>
		string.Equals(this.SelectionType, "folder", StringComparison.OrdinalIgnoreCase);

	private string PageTitleText => this.IsFolderSelection ? "Select Folder Tracks" : "Scan Disc";

	private string PageHeading => this.IsFolderSelection ? "Folder Tracks" : "Scan Disc";

	private string LoadingMessage => this.IsFolderSelection ? "Finding media files…" : "Scanning disc…";

	private string RefreshButtonText => this.IsFolderSelection ? "Refresh Files" : "Rescan Disc";

	protected override async Task OnInitializedAsync()
	{
		if (string.IsNullOrWhiteSpace(this.Root) || (!this.IsFolderSelection && string.IsNullOrWhiteSpace(this.Path)))
		{
			this._errorMessage = this.IsFolderSelection
				? "Missing required parameters: root must be provided."
				: "Missing required parameters: root and path must be provided.";
			return;
		}

		await this.RunScan();
	}

	private async Task RunScan(bool forceRescan = false)
	{
		this._isScanning = true;
		this._errorMessage = null;
		this._scanResult = null;
		this._folderFiles = [];

		try
		{
			if (this.IsFolderSelection)
			{
				this._folderFiles = await this.FilesystemService.GetMediaFilesRecursiveAsync(this.Root!, this.Path);
				return;
			}

			var physicalPath = this.FilesystemService.ResolvePhysicalPath(this.Root!, this.Path!);
			this._scanResult = await this.HandBrakeService.ScanAsync(physicalPath, forceRescan);

			if (!this._scanResult.IsSuccess)
			{
				this.Snackbar.Add("Scan failed.", Severity.Error);
			}
		}
		catch (ArgumentException)
		{
			this._errorMessage = "Invalid path: the specified path is not allowed.";
		}
		catch (Exception ex)
		{
			this._errorMessage = $"Scan error: {ex.Message}";
			this.Snackbar.Add(this._errorMessage, Severity.Error);
		}
		finally
		{
			this._isScanning = false;
		}
	}

	private void OnModeChanged(ScanMode mode)
	{
		this._selectedMode = mode;
	}

	private async Task ForceRescan() => await this.RunScan(forceRescan: true);
}
