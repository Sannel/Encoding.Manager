namespace Sannel.Encoding.Manager.Web.Features.Interlace.Options;

/// <summary>Interlace detection settings (config section <c>Interlace</c>).</summary>
public class InterlaceOptions
{
	/// <summary>Probe Blu-ray titles and media files with ffmpeg. DVDs are always interlaced and never probed.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// ffmpeg executable. Null = "ffmpeg" on PATH. On Windows use a "full" build with libbluray, e.g.
	/// <c>C:\ffmpeg\bin\ffmpeg.exe</c> (check with <c>ffmpeg -protocols</c>: it must list <c>bluray</c>).
	/// </summary>
	public string? FfmpegPath { get; set; }

	/// <summary>Points in the title / file (fractions of its duration) where frames are sampled.</summary>
	public double[] SamplePoints { get; set; } = [0.10, 0.45, 0.80];

	/// <summary>Frames decoded per sample point.</summary>
	public int FramesPerSample { get; set; } = 500;

	/// <summary>Kill an ffmpeg sample run after this many seconds.</summary>
	public int ProbeTimeoutSeconds { get; set; } = 120;

	/// <summary>ffmpeg processes run at the same time.</summary>
	public int MaxConcurrentProbes { get; set; } = 1;

	/// <summary>Interlaced frames (TFF + BFF) at or above this percentage of decided frames → interlaced.</summary>
	public double InterlacedThresholdPercent { get; set; } = 10;

	/// <summary>Frames with a repeated field at or above this percentage → telecined.</summary>
	public double TelecineThresholdPercent { get; set; } = 10;

	/// <summary>Fewer decided frames than this (e.g. black or static samples) → unknown.</summary>
	public int MinimumDecidedFrames { get; set; } = 50;
}
