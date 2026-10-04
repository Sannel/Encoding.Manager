namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A run of chapters returned by <c>get_title_chapters</c>.</summary>
public class McpChapterSegment
{
	public int Segment { get; init; }

	public int StartChapter { get; init; }

	public int EndChapter { get; init; }

	/// <summary>Duration as h:mm:ss.</summary>
	public string Duration { get; init; } = string.Empty;

	public int DurationSeconds { get; init; }
}
