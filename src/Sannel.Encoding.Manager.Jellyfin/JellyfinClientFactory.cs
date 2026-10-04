namespace Sannel.Encoding.Manager.Jellyfin;

public class JellyfinClientFactory : IJellyfinClientFactory
{
	private readonly IHttpClientFactory _httpClientFactory;

	public JellyfinClientFactory(IHttpClientFactory httpClientFactory) =>
		this._httpClientFactory = httpClientFactory;

	public IJellyfinClient CreateClient(string baseUrl, string apiKey)
	{
		var handler = new JellyfinAuthHandler(apiKey)
		{
			InnerHandler = new HttpClientHandler()
		};

		var httpClient = new HttpClient(handler, disposeHandler: true)
		{
			BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
		};

		return new JellyfinClient(httpClient);
	}
}
