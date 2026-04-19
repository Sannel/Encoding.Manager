using Microsoft.AspNetCore.Components;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class MovieFilesModeView : NamingComponentBase
{
	[Parameter]
	public required IReadOnlyList<FileEntryResponse> Files { get; set; }

	[Parameter]
	public string? DiscRootLabel { get; set; }

	[Parameter]
	public string? DiscRelativePath { get; set; }

	private sealed record MovieTrackItem(int Key, FileEntryResponse File);

	private bool _isAddingToQueue;

	private List<MovieTrackItem> TrackItems => this.Files
		.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
		.Select((file, index) => new MovieTrackItem(index + 1, file))
		.ToList();

	private NamingRowData GetMovieNamingRow(MovieTrackItem item)
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

	private async Task OnAddToQueueClicked()
	{
		this._isAddingToQueue = true;
		try
		{
			var tracks = this.TrackItems
				.Select(item =>
				{
					var row = this.GetMovieNamingRow(item);
					return new EncodeTrackConfig
					{
						TitleNumber = 1,
						SourceRelativePath = item.File.RelativePath,
						OutputName = string.IsNullOrWhiteSpace(row.Name)
							? string.Empty
							: row.Name.Trim(),
						SeasonNumber = null,
						EpisodeNumber = null,
						MovieYear = string.IsNullOrWhiteSpace(this._movieYear) ? null : this._movieYear.Trim(),
						Resolution = row.Resolution,
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
