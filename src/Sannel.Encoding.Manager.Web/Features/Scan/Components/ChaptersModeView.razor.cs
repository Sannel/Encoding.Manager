using Microsoft.AspNetCore.Components;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class ChaptersModeView : NamingComponentBase
{
	[Parameter]
	public required HandBrakeScanResult ScanResult { get; set; }

	private TitleInfo? _selectedTitle;
	private int _chaptersPerSegment = 1;

	private record ChapterSegment(int SegmentNumber, int StartChapter, int EndChapter, TimeSpan Duration);

	private List<TitleInfo> AllTitles =>
		this.ScanResult.Titles
			.OrderBy(t => t.TitleNumber)
			.ToList();

	private List<ChapterSegment> Segments
	{
		get
		{
			if (this._selectedTitle is null || this._selectedTitle.Chapters.Count == 0)
			{
				return [];
			}

			var chapters = this._selectedTitle.Chapters.OrderBy(c => c.ChapterNumber).ToList();
			var chunkSize = Math.Max(1, this._chaptersPerSegment);
			var segments = new List<ChapterSegment>();
			var segmentNumber = 1;

			for (var i = 0; i < chapters.Count; i += chunkSize)
			{
				var chunk = chapters.Skip(i).Take(chunkSize).ToList();
				var duration = chunk.Aggregate(TimeSpan.Zero, (sum, c) => sum + c.Duration);
				segments.Add(new ChapterSegment(
					segmentNumber++,
					chunk[0].ChapterNumber,
					chunk[^1].ChapterNumber,
					duration));
			}

			return segments;
		}
	}

	private TimeSpan TotalDuration =>
		this._selectedTitle is null
			? TimeSpan.Zero
			: this._selectedTitle.Chapters.Aggregate(TimeSpan.Zero, (sum, c) => sum + c.Duration);

	private void OnTitleSelected(TitleInfo? title)
	{
		this._selectedTitle = title;
		this._namingRows.Clear();
	}

	private void OnChaptersPerSegmentChanged(int value)
	{
		this._chaptersPerSegment = value;
		this._namingRows.Clear();
	}

	private void OnCascadeClicked() =>
		this.CascadeRows(this.Segments.Select(s => s.SegmentNumber).ToList());

	private bool _isAddingToQueue;

	private async Task OnAddToQueueClicked()
	{
		if (this._selectedTitle is null)
		{
			return;
		}

		this._isAddingToQueue = true;
		try
		{
			var titleNumber = this._selectedTitle.TitleNumber;
			var discName = Path.GetFileName(this.ScanResult.InputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			var tracks = this.Segments
				.Select(seg =>
				{
					var nr = this.GetNamingRow(seg.SegmentNumber);
					var fallback = seg.StartChapter == seg.EndChapter
						? $"{discName} Title {titleNumber} Ch {seg.StartChapter}"
						: $"{discName} Title {titleNumber} Ch {seg.StartChapter}-{seg.EndChapter}";
					return new EncodeTrackConfig
					{
						TitleNumber = titleNumber,
						StartChapter = seg.StartChapter,
						EndChapter = seg.EndChapter,
						OutputName = string.IsNullOrWhiteSpace(nr.Name) ? fallback : nr.Name,
						SeasonNumber = nr.Season,
						EpisodeNumber = nr.Episode?.EpisodeNumber,
					};
				})
				.ToList();
			await this.AddDiskToQueueAsync(this.ScanResult.InputPath, "Chapters", tracks);
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
		var seg = this.Segments.FirstOrDefault(s => s.SegmentNumber == key);
		if (seg is null || this._selectedTitle is null)
		{
			return string.Empty;
		}

		var discName = Path.GetFileName(this.ScanResult.InputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		return seg.StartChapter == seg.EndChapter
			? $"{discName} Title {this._selectedTitle.TitleNumber} Ch {seg.StartChapter}"
			: $"{discName} Title {this._selectedTitle.TitleNumber} Ch {seg.StartChapter}-{seg.EndChapter}";
	}
}
