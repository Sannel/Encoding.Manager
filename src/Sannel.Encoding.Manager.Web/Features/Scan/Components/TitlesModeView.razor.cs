using Microsoft.AspNetCore.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class TitlesModeView : NamingComponentBase
{
	[Parameter]
	public required HandBrakeScanResult ScanResult { get; set; }

	private int _minimumMinutes = 0;
	private int _minimumSeconds = 30;

	private TimeSpan MinimumDuration =>
		new TimeSpan(0, this._minimumMinutes, this._minimumSeconds);

	private List<TitleInfo> FilteredTitles =>
		this.ScanResult.Titles
			.Where(t => t.Duration >= this.MinimumDuration)
			.OrderBy(t => t.TitleNumber)
			.ToList();

	private void OnMinimumMinutesChanged(int value)
	{
		this._minimumMinutes = value;
	}

	private void OnMinimumSecondsChanged(int value)
	{
		this._minimumSeconds = value;
	}

	private void OnCascadeClicked() =>
		this.CascadeRows(this.FilteredTitles.Select(t => t.TitleNumber).ToList());

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
						SeasonNumber = nr.Season,
						EpisodeNumber = nr.Episode?.EpisodeNumber,
					};
				})
				.ToList();
			await this.AddDiskToQueueAsync(this.ScanResult.InputPath, "Titles", tracks);
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
		var discName = Path.GetFileName(this.ScanResult.InputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		return $"{discName} Title {key}";
	}
}
