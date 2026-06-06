using System.Net.Http.Headers;

namespace Sannel.Encoding.Manager.Jellyfin;

public class JellyfinAuthHandler : DelegatingHandler
{
	private readonly string _apiKey;

	public JellyfinAuthHandler(string apiKey) =>
		this._apiKey = apiKey;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		request.Headers.Remove("X-Emby-Token");
		request.Headers.Add("X-Emby-Token", this._apiKey);
		return base.SendAsync(request, cancellationToken);
	}
}
