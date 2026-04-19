using Microsoft.AspNetCore.Components;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class MovieTitlesModeView : NamingComponentBase
{
	[Parameter]
	public required HandBrakeScanResult ScanResult { get; set; }

	[Parameter]
	public string? DiscRootLabel { get; set; }

	[Parameter]
	public string? DiscRelativePath { get; set; }

	private int _minimumMinutes = 0;
	private int _minimumSeconds = 30;

	private TimeSpan MinimumDuration =>
		new TimeSpan(0, this._minimumMinutes, this._minimumSeconds);

	private List<TitleInfo> FilteredTitles =>
		this.ScanResult.Titles
			.Where(t => t.Duration >= this.MinimumDuration)
			.OrderBy(t => t.TitleNumber)
			.ToList();

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();
		foreach (var title in this.ScanResult.Titles)
		{
			var row = this.GetNamingRow(title.TitleNumber);
			if (row.Resolution is null)
			{
				this.ApplyDetectedResolution(title.TitleNumber, title.Width, title.Height);
			}
		}
	}

	private void OnMinimumMinutesChanged(int value)
	{
		this._minimumMinutes = value;
	}

	private void OnMinimumSecondsChanged(int value)
	{
		this._minimumSeconds = value;
	}

	private bool _isAddingToQueue;

	private async Task OnAddToQueueClicked()
	{
		this._isAddingToQueue = true;
		try
		{
			var tracks = this.FilteredTitles
				.Select(title =>
				{
					var nr = this.GetNamingRow(title.TitleNumber);
					return new EncodeTrackConfig
					{
						TitleNumber = title.TitleNumber,
						OutputName = nr.Name,
						SeasonNumber = null,
						EpisodeNumber = null,
						MovieYear = string.IsNullOrWhiteSpace(this._movieYear) ? null : this._movieYear.Trim(),
						Resolution = nr.Resolution,
					};
				})
				.ToList();
			await this.AddDiskToQueueAsync(
				this.DiscRootLabel is not null ? this.DiscRelativePath ?? this.ScanResult.InputPath : this.ScanResult.InputPath,
				this.DiscRootLabel,
				"Titles",
				tracks);
		}
		finally
		{
			this._isAddingToQueue = false;
		}
	}

	private static string FormatDuration(TimeSpan duration) =>
		$"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";

	protected override string GetFallbackAutoName(int key)
	{
		if (!string.IsNullOrEmpty(this._movieName))
		{
			return this._movieName;
		}

		var discName = Path.GetFileName(this.ScanResult.InputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		return $"{discName} Title {key}";
	}
}
