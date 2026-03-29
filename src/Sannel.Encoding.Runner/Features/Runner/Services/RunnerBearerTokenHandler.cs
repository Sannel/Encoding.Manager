using System.Net.Http.Headers;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

public class RunnerBearerTokenHandler : DelegatingHandler
{
	private readonly IRunnerAccessTokenProvider _tokenProvider;

	public RunnerBearerTokenHandler(IRunnerAccessTokenProvider tokenProvider)
	{
		_tokenProvider = tokenProvider;
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await base.SendAsync(request, cancellationToken);
	}
}
