namespace Sannel.Encoding.Manager.Web.Features.Interlace.Entities;

/// <summary>Cached ffmpeg <c>idet</c> result for one Blu-ray title or one media file.</summary>
public class InterlaceProbeCache
{
	/// <summary>Value of <see cref="Playlist"/> for a media file (not a Blu-ray title).</summary>
	public const int FilePlaylist = -1;

	public Guid Id { get; set; }

	/// <summary>Physical path of the disc folder or media file.</summary>
	public string SourcePath { get; set; } = string.Empty;

	/// <summary>
	/// Blu-ray playlist number, or <see cref="FilePlaylist"/> for a media file. Not nullable so the unique
	/// (<see cref="SourcePath"/>, <see cref="Playlist"/>) index also holds for files on PostgreSQL (NULLs never collide).
	/// </summary>
	public int Playlist { get; set; }

	/// <summary>File size (files) or the playlist's largest clip size (Blu-ray); a change re-probes.</summary>
	public long SourceSize { get; set; }

	/// <summary>Last write time of the probed file / clip; a change re-probes.</summary>
	public DateTimeOffset SourceLastWriteUtc { get; set; }

	/// <summary>Interlaced, Telecined, Mixed, Progressive or Unknown.</summary>
	public string Verdict { get; set; } = string.Empty;

	/// <summary>(TFF + BFF) / decided frames × 100.</summary>
	public double InterlacedPercent { get; set; }

	/// <summary>Repeated-field frames / decided frames × 100.</summary>
	public double TelecinePercent { get; set; }

	public int SampledFrames { get; set; }

	/// <summary>HandBrake's <c>InterlaceDetected</c> flag at probe time (Blu-ray titles only).</summary>
	public bool? HandBrakeDetected { get; set; }

	public string FfmpegVersion { get; set; } = string.Empty;

	/// <summary>Probe algorithm version; rows from an older version are treated as a cache miss.</summary>
	public int ProbeVersion { get; set; }

	public DateTimeOffset ProbedAt { get; set; }
}
