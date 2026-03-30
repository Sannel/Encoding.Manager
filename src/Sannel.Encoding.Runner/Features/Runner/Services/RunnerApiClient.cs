using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Runner.Features.Runner.Dto;
using Sannel.Encoding.Runner.Features.Runner.Options;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

public class RunnerApiClient : IRunnerApiClient, IAsyncDisposable
{
	private readonly HttpClient _http;
	private readonly IRunnerAccessTokenProvider _tokenProvider;
	private readonly ILogger<RunnerApiClient> _logger;
	private readonly Uri _serviceBaseUri;
	private readonly SemaphoreSlim _hubLock = new(1, 1);
	private HubConnection? _hubConnection;

	public RunnerApiClient(
		HttpClient http,
		IOptions<RunnerOptions> options,
		IRunnerAccessTokenProvider tokenProvider,
		ILogger<RunnerApiClient> logger)
	{
		_http = http;
		_tokenProvider = tokenProvider;
		_logger = logger;
		_serviceBaseUri = new Uri(options.Value.ServiceBaseUrl.TrimEnd('/') + "/");
		_http.BaseAddress = _serviceBaseUri;
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
		if (await TryUpdateJobStatusViaSignalRAsync(jobId, status, progressPercent, error, ct))
		{
			return;
		}

		var response = await _http.PutAsJsonAsync(
			$"api/runner/items/{jobId}/status",
			new { Status = status, ProgressPercent = progressPercent, Error = error },
			ct);
		await EnsureSuccessAsync(response, "update-status", ct);
	}

	private async Task<bool> TryUpdateJobStatusViaSignalRAsync(
		Guid jobId,
		string status,
		int? progressPercent,
		string? error,
		CancellationToken ct)
	{
		try
		{
			var hubConnection = await GetOrCreateHubConnectionAsync(ct);
			await hubConnection.InvokeAsync("UpdateJobStatus", jobId, status, progressPercent, error, ct);
			return true;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(
				ex,
				"SignalR status update failed for job {JobId}; falling back to HTTP.",
				jobId);

			await ResetHubConnectionAsync();
			return false;
		}
	}

	private async Task<HubConnection> GetOrCreateHubConnectionAsync(CancellationToken ct)
	{
		if (_hubConnection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
		{
			return _hubConnection;
		}

		await _hubLock.WaitAsync(ct);
		try
		{
			if (_hubConnection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
			{
				return _hubConnection;
			}

			if (_hubConnection is not null)
			{
				await DisposeHubConnectionAsync(_hubConnection);
			}

			_hubConnection = new HubConnectionBuilder()
				.WithUrl(new Uri(_serviceBaseUri, "hubs/runner-status"), options =>
				{
					options.AccessTokenProvider = async () => await _tokenProvider.GetAccessTokenAsync();
				})
				.WithAutomaticReconnect()
				.Build();

			await _hubConnection.StartAsync(ct);
			return _hubConnection;
		}
		finally
		{
			_hubLock.Release();
		}
	}

	private async Task ResetHubConnectionAsync()
	{
		await _hubLock.WaitAsync();
		try
		{
			if (_hubConnection is null)
			{
				return;
			}

			await DisposeHubConnectionAsync(_hubConnection);
			_hubConnection = null;
		}
		finally
		{
			_hubLock.Release();
		}
	}

	private static async Task DisposeHubConnectionAsync(HubConnection hubConnection)
	{
		try
		{
			await hubConnection.StopAsync();
		}
		catch
		{
			// Ignore teardown failures; caller is already recovering.
		}

		await hubConnection.DisposeAsync();
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

	public async ValueTask DisposeAsync()
	{
		if (_hubConnection is not null)
		{
			await DisposeHubConnectionAsync(_hubConnection);
			_hubConnection = null;
		}

		_hubLock.Dispose();
	}
}
