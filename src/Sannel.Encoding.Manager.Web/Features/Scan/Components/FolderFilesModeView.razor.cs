using Microsoft.AspNetCore.Components;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class FolderFilesModeView : NamingComponentBase
{
	[Parameter]
	public required IReadOnlyList<FileEntryResponse> Files { get; set; }

	[Parameter]
	public string? DiscRootLabel { get; set; }

	[Parameter]
	public string? DiscRelativePath { get; set; }

	private sealed record FolderTrackItem(int Key, FileEntryResponse File);

	private bool _isAddingToQueue;

	private List<FolderTrackItem> TrackItems => this.Files
		.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
		.Select((file, index) => new FolderTrackItem(index + 1, file))
		.ToList();

	private NamingRowData GetFolderNamingRow(FolderTrackItem item)
	{
		return this.GetNamingRow(item.Key);
	}

	public void ClearAllTrackNames()
	{
		foreach (var item in this.TrackItems)
		{
			var row = this.GetNamingRow(item.Key);
			row.Name = string.Empty;
		}

		this.StateHasChanged();
	}

	private void OnCascadeClicked() =>
		this.CascadeRows(this.TrackItems.Select(item => item.Key).ToList());

	private async Task OnAddToQueueClicked()
	{
		this._isAddingToQueue = true;
		try
		{
			var tracks = this.TrackItems
				.Select(item =>
				{
					var row = this.GetFolderNamingRow(item);
					return new EncodeTrackConfig
					{
						TitleNumber = 1,
						SourceRelativePath = item.File.RelativePath,
						OutputName = string.IsNullOrWhiteSpace(row.Name)
							? string.Empty
							: row.Name.Trim(),
						SeasonNumber = row.Season,
						EpisodeNumber = row.Episode?.EpisodeNumber,
					};
				})
				.ToList();

			await this.AddDiskToQueueAsync(this.DiscRelativePath ?? string.Empty, this.DiscRootLabel, "Files", tracks);
		}
		finally
		{
			this._isAddingToQueue = false;
		}
	}

	private static string FormatBytes(long bytes)
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

	protected override string GetFallbackAutoName(int key)
	{
		var item = this.TrackItems.FirstOrDefault(track => track.Key == key);
		return item is null ? string.Empty : Path.GetFileNameWithoutExtension(item.File.Name);
	}
}