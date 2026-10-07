using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Utilities;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools mirroring the Scan page's disc scanning.</summary>
[McpServerToolType]
public class ScanTools
{
	internal const int PollAfterSeconds = 15;

	private readonly IBackgroundScanCoordinator _scanCoordinator;
	private readonly IForcedRescanLimiter _rescanLimiter;
	private readonly McpCaller _caller;
	private readonly McpOptions _options;
	private readonly IInterlaceService _interlace;

	public ScanTools(
		IBackgroundScanCoordinator scanCoordinator,
		IForcedRescanLimiter rescanLimiter,
		McpCaller caller,
		IOptions<McpOptions> options,
		IInterlaceService interlace)
	{
		this._interlace = interlace;
		this._scanCoordinator = scanCoordinator;
		this._rescanLimiter = rescanLimiter;
		this._caller = caller;
		this._options = options.Value;
	}

	[McpServerTool(Name = "scan_disc", ReadOnly = true)]
	[Description("""
		Scans a DVD or Blu-ray disc folder with HandBrake and returns its titles. Results are cached for 24 hours, so repeat
		scans are instant. An uncached scan can take minutes: if status is "Scanning", wait pollAfterSeconds and call
		get_scan_status. Only use forceRescan when the user asks for it — it is rate-limited.
		Each title has an interlace verdict and a recommendedPreset: DVD titles are always interlaced; Blu-ray titles are
		checked one by one (extras are often interlaced while the feature is not). While any title's interlace is "pending",
		wait pollAfterSeconds and call get_scan_status again. Use each title's recommendedPreset as that track's presetLabel.
		""")]
	public async Task<McpScanResult> ScanDiscAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder (a directory whose discType is DVD or BluRay).")] string path,
		[Description("Bypass the 24-hour scan cache. Rate-limited.")] bool forceRescan = false,
		[Description("Hide titles shorter than this many seconds (the Scan page default is 30).")] int minimumDurationSeconds = 30,
		CancellationToken ct = default)
	{
		if (forceRescan && !this._rescanLimiter.TryAcquire(this._caller.CallerId, out var retryAfter))
		{
			var minutes = Math.Max(1, (int)Math.Ceiling((retryAfter ?? TimeSpan.FromMinutes(1)).TotalMinutes));
			throw new McpException($"Forced rescan limit reached ({this._options.ForcedRescanPermitLimit} per {this._options.ForcedRescanWindowMinutes} minutes). Try again in about {minutes} minute(s), or call scan_disc without forceRescan to use the cached result.");
		}

		var status = await McpToolHelpers.GuardAsync(() => this._scanCoordinator.StartOrGetAsync(
			root,
			McpToolHelpers.NormalizePath(path),
			forceRescan,
			TimeSpan.FromSeconds(Math.Max(0, this._options.ScanWaitSeconds)),
			ct));
		return await this.ToResultAsync(status, minimumDurationSeconds, ct);
	}

	[McpServerTool(Name = "get_scan_status", ReadOnly = true, Idempotent = true)]
	[Description("Returns the status of a disc scan started with scan_disc (and the titles' interlace verdicts — poll while any is \"pending\"). Same result shape as scan_disc.")]
	public async Task<McpScanResult> GetScanStatusAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder.")] string path,
		[Description("Hide titles shorter than this many seconds.")] int minimumDurationSeconds = 30,
		CancellationToken ct = default)
	{
		var status = McpToolHelpers.Guard(() => this._scanCoordinator.GetStatus(root, McpToolHelpers.NormalizePath(path)))
			?? throw new McpException("No scan has been started for this disc. Call scan_disc first.");
		return await this.ToResultAsync(status, minimumDurationSeconds, ct);
	}

	[McpServerTool(Name = "get_title_chapters", ReadOnly = true, Idempotent = true)]
	[Description("Lists a scanned title's chapters grouped into segments of chaptersPerSegment chapters (the Scan page's Chapters mode). Use a segment's startChapter/endChapter when queuing in mode \"Chapters\".")]
	public async Task<IReadOnlyList<McpChapterSegment>> GetTitleChaptersAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder.")] string path,
		[Description("HandBrake title number from scan_disc.")] int titleNumber,
		[Description("Chapters per segment (1 = one segment per chapter).")] int chaptersPerSegment = 1,
		CancellationToken ct = default)
	{
		var title = await this.GetScannedTitleAsync(root, path, titleNumber, ct);
		var chapters = title.Chapters.OrderBy(c => c.ChapterNumber).ToList();
		var size = Math.Max(1, chaptersPerSegment);
		var segments = new List<McpChapterSegment>();
		for (var i = 0; i < chapters.Count; i += size)
		{
			var chunk = chapters.Skip(i).Take(size).ToList();
			var duration = chunk.Aggregate(TimeSpan.Zero, (sum, c) => sum + c.Duration);
			segments.Add(new McpChapterSegment
			{
				Segment = segments.Count + 1,
				StartChapter = chunk[0].ChapterNumber,
				EndChapter = chunk[^1].ChapterNumber,
				Duration = McpToolHelpers.FormatDuration(duration),
				DurationSeconds = (int)duration.TotalSeconds,
			});
		}

		return segments;
	}

	[McpServerTool(Name = "list_resolutions", ReadOnly = true, Idempotent = true)]
	[Description("Lists the resolution values accepted for movie tracks in queue_encode_job.")]
	public IReadOnlyList<string> ListResolutions() => ResolutionDetector.GetAvailableResolutions();

	private async Task<TitleInfo> GetScannedTitleAsync(string root, string path, int titleNumber, CancellationToken ct)
	{
		var status = await McpToolHelpers.GuardAsync(() => this._scanCoordinator.StartOrGetAsync(
			root,
			McpToolHelpers.NormalizePath(path),
			false,
			TimeSpan.FromSeconds(Math.Max(0, this._options.ScanWaitSeconds)),
			ct));
		if (status.State != BackgroundJobState.Completed || status.Result is null)
		{
			throw new McpException(status.State == BackgroundJobState.Failed
				? $"The disc scan failed: {status.Error}"
				: "The disc is still being scanned. Poll get_scan_status, then try again.");
		}

		return status.Result.Titles.FirstOrDefault(t => t.TitleNumber == titleNumber)
			?? throw new McpException($"Title {titleNumber} does not exist on this disc. Valid titles: {string.Join(", ", status.Result.Titles.Select(t => t.TitleNumber))}.");
	}

	private async Task<McpScanResult> ToResultAsync(ScanJobStatus status, int minimumDurationSeconds, CancellationToken ct)
	{
		switch (status.State)
		{
			case BackgroundJobState.Running:
				return new McpScanResult { Status = "Scanning", StartedAt = status.StartedAt, PollAfterSeconds = PollAfterSeconds };
			case BackgroundJobState.Failed:
				return new McpScanResult { Status = "Failed", StartedAt = status.StartedAt, Error = status.Error };
		}

		var minimum = TimeSpan.FromSeconds(Math.Max(0, minimumDurationSeconds));
		var all = status.Result?.Titles ?? [];
		var shown = all.Where(t => t.Duration >= minimum).OrderBy(t => t.TitleNumber).ToList();
		var verdicts = await this._interlace.GetDiscTitlesAsync(status.PhysicalPath, shown, ct);
		var titles = shown.Select(t => ToSummary(t, verdicts.GetValueOrDefault(t.TitleNumber))).ToList();
		return new McpScanResult
		{
			Status = "Completed",
			StartedAt = status.StartedAt,
			PollAfterSeconds = titles.Any(t => t.Interlace == "pending") ? InterlaceMapping.PendingPollSeconds : null,
			Titles = titles,
			HiddenShortTitles = all.Count - titles.Count,
		};
	}

	internal static McpTitleSummary ToSummary(TitleInfo title, InterlaceResult? interlace) => new()
	{
		Interlace = interlace is null ? "unknown" : InterlaceMapping.ToMcp(interlace.Verdict),
		InterlaceSource = interlace?.Source,
		InterlacedPercent = interlace?.InterlacedPercent,
		TelecinePercent = interlace?.TelecinePercent,
		RecommendedPreset = interlace?.RecommendedPreset,
		TitleNumber = title.TitleNumber,
		Playlist = title.Playlist,
		Duration = McpToolHelpers.FormatDuration(title.Duration),
		DurationSeconds = (int)title.Duration.TotalSeconds,
		Width = title.Width,
		Height = title.Height,
		DetectedResolution = ResolutionDetector.DetectResolution(title.Width, title.Height),
		FrameRate = Math.Round(title.FrameRate, 3),
		ChapterCount = title.Chapters.Count,
		AudioTracks = title.AudioTracks
			.Select(a => new McpAudioTrackDto { TrackNumber = a.TrackNumber, Language = a.Language, Codec = a.Codec, ChannelLayout = a.ChannelLayout })
			.ToList(),
		Subtitles = title.Subtitles
			.Select(s => new McpSubtitleDto { TrackNumber = s.TrackNumber, Language = s.Language, Format = s.Format })
			.ToList(),
	};
}
