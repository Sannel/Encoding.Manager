namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Audio track metadata.</summary>
public class AudioTrackInfo
{
	public int TrackNumber { get; init; }
	public string Codec { get; init; } = string.Empty;
	public string Language { get; init; } = string.Empty;
	public int SampleRate { get; init; }
	public int Bitrate { get; init; }
	public string ChannelLayout { get; init; } = string.Empty;
}
