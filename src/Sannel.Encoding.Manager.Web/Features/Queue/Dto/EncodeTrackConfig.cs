namespace Sannel.Encoding.Manager.Web.Features.Queue.Dto;

/// <summary>Per-track configuration within a disk-level queue job.</summary>
public class EncodeTrackConfig
{
	public int TitleNumber { get; set; }
	public int? StartChapter { get; set; }
	public int? EndChapter { get; set; }
	/// <summary>User-facing output filename (without extension). Empty means skip this track.</summary>
	public string OutputName { get; set; } = string.Empty;
	public int? SeasonNumber { get; set; }
	public int? EpisodeNumber { get; set; }
}
