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

	/// <summary>TVDB series ID used for template variable expansion.</summary>
	public int? TvdbId { get; set; }

	/// <summary>"Opus" or "Aac".</summary>
	public string AudioDefault { get; set; } = "Opus";

	/// <summary>
	/// JSON-serialized array of EncodeTrackConfig — only tracks with a non-empty OutputName.
	/// </summary>
	public string TracksJson { get; set; } = "[]";

	/// <summary>"Queued", "Encoding", "Finished", or "Failed".</summary>
	public string Status { get; set; } = "Queued";

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Name of the runner that claimed this job.</summary>
	public string? RunnerName { get; set; }

	/// <summary>When encoding started.</summary>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>When encoding completed or failed.</summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Filesystem root label for <see cref="DiscPath"/>.
	/// When set, DiscPath is root-relative (forward-slash). When null, DiscPath is an absolute path.
	/// </summary>
	public string? DiscRootLabel { get; set; }

	/// <summary>Encoding progress percentage (0-100) reported by the runner.</summary>
	public int? ProgressPercent { get; set; }

	/// <summary>Current file/track progress percentage (0-100) reported by the runner.</summary>
	public int? CurrentTrackProgressPercent { get; set; }

	/// <summary>
	/// JSON-serialized list of per-track encoding commands (sanitized with label paths).
	/// </summary>
	public string? EncodingCommandsJson { get; set; }

	/// <summary>
	/// True when this item has been cleared from default queue view.
	/// </summary>
	public bool IsArchived { get; set; }

	/// <summary>
	/// User-defined sort position. Lower value = higher priority. Assigned on add; swapped on reorder.
	/// </summary>
	public int SortOrder { get; set; }

	// ── Jellyfin integration (all nullable — non-Jellyfin jobs leave these null) ──

	/// <summary>FK → JellyfinServer used as the source.</summary>
	public Guid? JellyfinSourceServerId { get; set; }

	/// <summary>Item ID on the source Jellyfin server.</summary>
	public string? JellyfinSourceItemId { get; set; }

	/// <summary>FK → JellyfinServer to refresh after encode.</summary>
	public Guid? JellyfinDestServerId { get; set; }

	/// <summary>FK → JellyfinDestinationRoot selected for SFTP delivery.</summary>
	public Guid? JellyfinDestRootId { get; set; }

	/// <summary>Auto-built relative path within the destination root.</summary>
	public string? JellyfinDestRelativePath { get; set; }

	/// <summary>"Pending", "Uploading", "Uploaded", or "Failed".</summary>
	public string? JellyfinUploadStatus { get; set; }
}
