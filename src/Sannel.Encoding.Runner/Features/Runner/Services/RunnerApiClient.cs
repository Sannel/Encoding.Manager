using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
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
		await EnsureSuccessAsync(response, "heartbeat", ct);
	}

	public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
	{
		var responseMessage = await _http.GetAsync($"api/runner/{Uri.EscapeDataString(name)}/enabled", ct);
		var response = await ReadJsonAsync<RunnerStatusResponse>(responseMessage, "is-enabled", ct);
		return response?.IsEnabled ?? false;
	}

	public async Task<ClaimedJobResponse?> ClaimNextJobAsync(string name, CancellationToken ct = default)
	{
		var response = await _http.PostAsJsonAsync("api/runner/claim-next", new { RunnerName = name }, ct);

		if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
		{
			return null;
		}

		return await ReadJsonAsync<ClaimedJobResponse>(response, "claim-next", ct);
	}

	public async Task UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent = null, string? error = null, CancellationToken ct = default)
	{
		var response = await _http.PutAsJsonAsync(
			$"api/runner/items/{jobId}/status",
			new { Status = status, ProgressPercent = progressPercent, Error = error },
			ct);
		await EnsureSuccessAsync(response, "update-status", ct);
	}

	private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var body = await response.Content.ReadAsStringAsync(ct);
		var snippet = BuildSnippet(body);

		_logger.LogError(
			"Runner API request failed during {Operation}. StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}, ContentType={ContentType}, BodySnippet={BodySnippet}",
			operation,
			(int)response.StatusCode,
			response.ReasonPhrase,
			response.Content.Headers.ContentType?.MediaType,
			snippet);

		throw new HttpRequestException(
			$"Runner API request '{operation}' failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {snippet}");
	}

	private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct)
	{
		await EnsureSuccessAsync(response, operation, ct);

		var body = await response.Content.ReadAsStringAsync(ct);
		if (string.IsNullOrWhiteSpace(body))
		{
			return default;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
		}
		catch (JsonException ex)
		{
			var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
			var snippet = BuildSnippet(body);
			_logger.LogError(
				ex,
				"Runner API response was not valid JSON during {Operation}. ContentType={ContentType}, BodySnippet={BodySnippet}",
				operation,
				contentType,
				snippet);

			throw new InvalidOperationException(
				$"Runner API '{operation}' returned invalid JSON. Content-Type: {contentType}. Body: {snippet}", ex);
		}
	}

	private static string BuildSnippet(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return "<empty>";
		}

		var normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();
		return normalized.Length <= 300
			? normalized
			: normalized[..300] + "...";
	}
}
