namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Subtitle track metadata.</summary>
public class SubtitleInfo
{
	public int TrackNumber { get; init; }
	public string Language { get; init; } = string.Empty;
	public string Format { get; init; } = string.Empty;
}
