namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;

/// <summary>
/// View model for the destination root dropdown in queue dialogs.
/// Represents either a persisted JellyfinDestinationRoot (when RootId is set)
/// or a Jellyfin virtual folder path that will be auto-created on queue (when RootId is null).
/// </summary>
public sealed record DestinationRootOption(
	Guid? RootId,
	string DisplayName,
	string RootPath,
	Guid ServerId,
	string? LibraryName);
