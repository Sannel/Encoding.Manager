namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>One title from a HandBrake disc scan.</summary>
public class McpTitleSummary
{
	/// <summary>HandBrake title number — use as <c>titleNumber</c> when queuing.</summary>
	public int TitleNumber { get; init; }

	/// <summary>Blu-ray playlist (NNNNN.mpls) number, when known.</summary>
	public int? Playlist { get; init; }

	/// <summary>Duration as h:mm:ss.</summary>
	public string Duration { get; init; } = string.Empty;

	public int DurationSeconds { get; init; }

	public int Width { get; init; }

	public int Height { get; init; }

	/// <summary>Closest standard resolution ("480p", "720p", "1080p", "4k").</summary>
	public string DetectedResolution { get; init; } = string.Empty;

	public double FrameRate { get; init; }

	public int ChapterCount { get; init; }

	public IReadOnlyList<McpAudioTrackDto> AudioTracks { get; init; } = [];

	public IReadOnlyList<McpSubtitleDto> Subtitles { get; init; } = [];

	/// <summary>"interlaced", "telecined", "mixed", "progressive", "pending" (probe still running — poll) or "unknown".</summary>
	public string Interlace { get; init; } = "unknown";

	/// <summary>"dvd" (always interlaced), "handbrake+ffmpeg", or "handbrake" (ffmpeg unavailable); null while pending.</summary>
	public string? InterlaceSource { get; init; }

	public double? InterlacedPercent { get; init; }

	public double? TelecinePercent { get; init; }

	/// <summary>Preset this title's verdict suggests; null while pending. Use it as the track's presetLabel.</summary>
	public string? RecommendedPreset { get; init; }
}
