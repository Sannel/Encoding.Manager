using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Sannel.Encoding.Manager.Jellyfin.Dto;

namespace Sannel.Encoding.Manager.Jellyfin;

public class JellyfinClient : IJellyfinClient
{
	private readonly HttpClient _httpClient;
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public JellyfinClient(HttpClient httpClient) =>
		this._httpClient = httpClient;

	public async Task<JellyfinSystemInfo?> GetSystemInfoAsync(CancellationToken ct = default) =>
		await this._httpClient.GetFromJsonAsync<JellyfinSystemInfo>("System/Info", JsonOptions, ct).ConfigureAwait(false);

	public async Task<ItemsResponse> GetItemsAsync(GetItemsRequest request, CancellationToken ct = default)
	{
		var query = HttpUtility.ParseQueryString(string.Empty);
		query["Recursive"] = request.Recursive.ToString();
		query["Fields"] = request.Fields;

		if (!string.IsNullOrEmpty(request.IncludeItemTypes))
		{
			query["IncludeItemTypes"] = request.IncludeItemTypes;
		}

		if (!string.IsNullOrEmpty(request.ParentId))
		{
			query["ParentId"] = request.ParentId;
		}

		if (!string.IsNullOrEmpty(request.SearchTerm))
		{
			query["SearchTerm"] = request.SearchTerm;
		}

		if (request.StartIndex.HasValue)
		{
			query["StartIndex"] = request.StartIndex.Value.ToString();
		}

		if (request.Limit.HasValue)
		{
			query["Limit"] = request.Limit.Value.ToString();
		}

		if (!string.IsNullOrEmpty(request.AnyProviderIdEquals))
		{
			query["AnyProviderIdEquals"] = request.AnyProviderIdEquals;
		}

		var url = $"Items?{query}";
		return await this._httpClient.GetFromJsonAsync<ItemsResponse>(url, JsonOptions, ct).ConfigureAwait(false)
			?? new ItemsResponse();
	}

	public async Task<JellyfinItem?> GetItemAsync(string itemId, string? userId = null, CancellationToken ct = default)
	{
		var fields = "ProviderIds,SeriesProviderIds,Path";
		var url = userId is not null
			? $"Users/{HttpUtility.UrlEncode(userId)}/Items/{HttpUtility.UrlEncode(itemId)}?Fields={fields}"
			: $"Items/{HttpUtility.UrlEncode(itemId)}?Fields={fields}";
		return await this._httpClient.GetFromJsonAsync<JellyfinItem>(url, JsonOptions, ct).ConfigureAwait(false);
	}

	public async Task DownloadItemAsync(string itemId, Stream destination, CancellationToken ct = default)
	{
		var url = $"Items/{HttpUtility.UrlEncode(itemId)}/Download";
		using var response = await this._httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		await stream.CopyToAsync(destination, ct).ConfigureAwait(false);
	}

	public async Task RefreshLibraryAsync(CancellationToken ct = default)
	{
		var response = await this._httpClient.PostAsync("Library/Refresh", null, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}

	public async Task<IReadOnlyList<JellyfinVirtualFolder>> GetVirtualFoldersAsync(CancellationToken ct = default) =>
		await this._httpClient.GetFromJsonAsync<List<JellyfinVirtualFolder>>("Library/VirtualFolders", JsonOptions, ct).ConfigureAwait(false)
			?? [];

	public async Task<IReadOnlyList<JellyfinUser>> GetUsersAsync(CancellationToken ct = default) =>
		await this._httpClient.GetFromJsonAsync<List<JellyfinUser>>("Users", JsonOptions, ct).ConfigureAwait(false)
			?? [];

	public async Task<ItemsResponse> GetUserItemsAsync(string userId, GetItemsRequest request, CancellationToken ct = default)
	{
		var query = HttpUtility.ParseQueryString(string.Empty);
		query["Recursive"] = request.Recursive.ToString();
		query["Fields"] = request.Fields;

		if (!string.IsNullOrEmpty(request.IncludeItemTypes))
		{
			query["IncludeItemTypes"] = request.IncludeItemTypes;
		}

		if (!string.IsNullOrEmpty(request.ParentId))
		{
			query["ParentId"] = request.ParentId;
		}

		if (!string.IsNullOrEmpty(request.SearchTerm))
		{
			query["SearchTerm"] = request.SearchTerm;
		}

		if (request.StartIndex.HasValue)
		{
			query["StartIndex"] = request.StartIndex.Value.ToString();
		}

		if (request.Limit.HasValue)
		{
			query["Limit"] = request.Limit.Value.ToString();
		}

		if (!string.IsNullOrEmpty(request.AnyProviderIdEquals))
		{
			query["AnyProviderIdEquals"] = request.AnyProviderIdEquals;
		}

		var url = $"Users/{HttpUtility.UrlEncode(userId)}/Items?{query}";
		return await this._httpClient.GetFromJsonAsync<ItemsResponse>(url, JsonOptions, ct).ConfigureAwait(false)
			?? new ItemsResponse();
	}

	public async Task<JellyfinUserData?> GetUserDataAsync(string userId, string itemId, CancellationToken ct = default)
	{
		var url = $"Users/{HttpUtility.UrlEncode(userId)}/Items/{HttpUtility.UrlEncode(itemId)}?Fields=UserData";
		var item = await this._httpClient.GetFromJsonAsync<JellyfinItem>(url, JsonOptions, ct).ConfigureAwait(false);
		return item?.UserData;
	}

	public async Task MarkPlayedAsync(string userId, string itemId, DateTimeOffset? datePlayed = null, CancellationToken ct = default)
	{
		var url = $"Users/{HttpUtility.UrlEncode(userId)}/PlayedItems/{HttpUtility.UrlEncode(itemId)}";
		if (datePlayed.HasValue)
		{
			url += $"?DatePlayed={Uri.EscapeDataString(datePlayed.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";
		}

		var response = await this._httpClient.PostAsync(url, null, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}

	public async Task MarkUnplayedAsync(string userId, string itemId, CancellationToken ct = default)
	{
		var url = $"Users/{HttpUtility.UrlEncode(userId)}/PlayedItems/{HttpUtility.UrlEncode(itemId)}";
		var response = await this._httpClient.DeleteAsync(url, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}

	public async Task UpdatePlaybackPositionAsync(string userId, string itemId, long positionTicks, DateTimeOffset? lastPlayedDate = null, CancellationToken ct = default)
	{
		var url = $"Users/{HttpUtility.UrlEncode(userId)}/Items/{HttpUtility.UrlEncode(itemId)}/UserData";
		var payload = new { PlaybackPositionTicks = positionTicks, LastPlayedDate = lastPlayedDate?.UtcDateTime };
		var response = await this._httpClient.PostAsJsonAsync(url, payload, JsonOptions, ct).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}
}
