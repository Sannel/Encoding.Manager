namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Video stream metadata.</summary>
public class VideoStreamInfo
{
	public string Codec { get; init; } = string.Empty;
	public int Width { get; init; }
	public int Height { get; init; }
	public double FrameRate { get; init; }
}
