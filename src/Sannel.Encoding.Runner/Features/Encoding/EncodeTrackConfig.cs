namespace Sannel.Encoding.Runner.Features.Encoding;

public class EncodeTrackConfig
{
	public int TitleNumber { get; set; }
	public int? StartChapter { get; set; }
	public int? EndChapter { get; set; }
	public string? SourceRelativePath { get; set; }
	public string OutputName { get; set; } = string.Empty;
	public int? SeasonNumber { get; set; }
	public int? EpisodeNumber { get; set; }
	public string? MovieYear { get; set; }
	public string? Resolution { get; set; }
	public string? PresetLabel { get; set; }
}
