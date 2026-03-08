namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>Per-title metadata from a scan.</summary>
public class TitleInfo
{
	public int TitleNumber { get; init; }
	public TimeSpan Duration { get; init; }
	public IReadOnlyList<VideoStreamInfo> VideoStreams { get; init; } = [];
	public IReadOnlyList<AudioTrackInfo> AudioTracks { get; init; } = [];
	public IReadOnlyList<SubtitleInfo> Subtitles { get; init; } = [];
	public IReadOnlyList<ChapterInfo> Chapters { get; init; } = [];
	public double FrameRate { get; init; }
	public int Width { get; init; }
	public int Height { get; init; }
}
