namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>Incremental progress updates during encoding.</summary>
public class ProgressInfo
{
	public double Percent { get; init; }
	public string CurrentPhase { get; init; } = string.Empty;
	public double CurrentFps { get; init; }
	public double AverageFps { get; init; }
	public TimeSpan? Eta { get; init; }
}
