using Sannel.Encoding.Manager.Jellyfin.Dto;

namespace Sannel.Encoding.Manager.Jellyfin;

public interface IJellyfinClient
{
	Task<JellyfinSystemInfo?> GetSystemInfoAsync(CancellationToken ct = default);
	Task<ItemsResponse> GetItemsAsync(GetItemsRequest request, CancellationToken ct = default);
	Task<JellyfinItem?> GetItemAsync(string itemId, string? userId = null, CancellationToken ct = default);
	Task DownloadItemAsync(string itemId, Stream destination, CancellationToken ct = default);
	Task RefreshLibraryAsync(CancellationToken ct = default);
	Task<IReadOnlyList<JellyfinVirtualFolder>> GetVirtualFoldersAsync(CancellationToken ct = default);
	Task<IReadOnlyList<JellyfinUser>> GetUsersAsync(CancellationToken ct = default);
	Task<ItemsResponse> GetUserItemsAsync(string userId, GetItemsRequest request, CancellationToken ct = default);
	Task<JellyfinUserData?> GetUserDataAsync(string userId, string itemId, CancellationToken ct = default);
	Task MarkPlayedAsync(string userId, string itemId, DateTimeOffset? datePlayed = null, CancellationToken ct = default);
	Task MarkUnplayedAsync(string userId, string itemId, CancellationToken ct = default);
	Task UpdatePlaybackPositionAsync(string userId, string itemId, long positionTicks, DateTimeOffset? lastPlayedDate = null, CancellationToken ct = default);
}
