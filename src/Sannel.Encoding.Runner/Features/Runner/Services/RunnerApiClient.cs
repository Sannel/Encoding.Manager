using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Sannel.Encoding.Runner.Features.Runner.Dto;
using Sannel.Encoding.Runner.Features.Runner.Options;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

public class RunnerApiClient : IRunnerApiClient
{
	private readonly HttpClient _http;
	private readonly ILogger<RunnerApiClient> _logger;

	public RunnerApiClient(HttpClient http, IOptions<RunnerOptions> options, ILogger<RunnerApiClient> logger)
	{
		_http = http;
		_logger = logger;
		_http.BaseAddress = new Uri(options.Value.ServiceBaseUrl.TrimEnd('/') + "/");
	}

	public async Task SendHeartbeatAsync(string name, CancellationToken ct = default)
	{
		var response = await _http.PostAsJsonAsync("api/runner/heartbeat", new { RunnerName = name }, ct);
		response.EnsureSuccessStatusCode();
	}

	public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
	{
		var response = await _http.GetFromJsonAsync<RunnerStatusResponse>(
			$"api/runner/{Uri.EscapeDataString(name)}/enabled", ct);
		return response?.IsEnabled ?? false;
	}

	public async Task<ClaimedJobResponse?> ClaimNextJobAsync(string name, CancellationToken ct = default)
	{
		var response = await _http.PostAsJsonAsync("api/runner/claim-next", new { RunnerName = name }, ct);

		if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<ClaimedJobResponse>(ct);
	}

	public async Task UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent = null, string? error = null, CancellationToken ct = default)
	{
		var response = await _http.PutAsJsonAsync(
			$"api/runner/items/{jobId}/status",
			new { Status = status, ProgressPercent = progressPercent, Error = error },
			ct);
		response.EnsureSuccessStatusCode();
	}
}
