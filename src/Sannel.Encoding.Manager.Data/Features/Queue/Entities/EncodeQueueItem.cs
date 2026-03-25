namespace Sannel.Encoding.Manager.Web.Features.Queue.Entities;

/// <summary>A disk-level encode job. Individual track configs are stored in <see cref="TracksJson"/>.</summary>
public class EncodeQueueItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Physical path to the disc image / directory.</summary>
	public string DiscPath { get; set; } = string.Empty;

	/// <summary>"Titles" or "Chapters".</summary>
	public string Mode { get; set; } = "Titles";

	public string? TvdbShowName { get; set; }

	/// <summary>"Opus" or "Aac".</summary>
	public string AudioDefault { get; set; } = "Opus";

	/// <summary>
	/// JSON-serialized array of EncodeTrackConfig — only tracks with a non-empty OutputName.
	/// </summary>
	public string TracksJson { get; set; } = "[]";

	/// <summary>"Queued", "Encoding", "Done", or "Failed".</summary>
	public string Status { get; set; } = "Queued";

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
