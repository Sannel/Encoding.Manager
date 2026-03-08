namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>Chapter marker metadata.</summary>
public class ChapterInfo
{
	public int ChapterNumber { get; init; }
	public string Name { get; init; } = string.Empty;
	public TimeSpan Duration { get; init; }
}
