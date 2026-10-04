namespace Sannel.Encoding.Manager.Jellyfin;

public interface IJellyfinClientFactory
{
	IJellyfinClient CreateClient(string baseUrl, string apiKey);
}
