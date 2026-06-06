namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

/// <summary>
/// View model for the destination root dropdown in queue dialogs.
/// Represents either a persisted JellyfinDestinationRoot (when RootId is set),
/// a Jellyfin virtual folder path that will be auto-created on queue (when RootId is null),
/// or the runner's local encode destination (when IsLocal is true).
/// </summary>
public sealed record DestinationRootOption(
	Guid? RootId,
	string DisplayName,
	string RootPath,
	Guid ServerId,
	string? LibraryName)
{
	/// <summary>True when this option represents the runner's local encode destination (no SFTP upload).</summary>
	public bool IsLocal => this.ServerId == Guid.Empty && this.RootId is null;

	/// <summary>The local encode destination option — encodes directly to the runner's TrackDestinationRoot.</summary>
	public static DestinationRootOption LocalDestination { get; } =
		new(null, "Local Encode Destination", string.Empty, Guid.Empty, null);
}
