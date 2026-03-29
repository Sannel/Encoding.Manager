using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

public class RunnerAccessTokenProvider : IRunnerAccessTokenProvider
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<RunnerAccessTokenProvider> _logger;
	private readonly SemaphoreSlim _tokenLock = new(1, 1);

	private string? _accessToken;
	private DateTimeOffset _expiresAtUtc;

	public RunnerAccessTokenProvider(IConfiguration configuration, ILogger<RunnerAccessTokenProvider> logger)
	{
		_configuration = configuration;
		_logger = logger;
	}

	public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
	{
		if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
		{
			return _accessToken;
		}

		await _tokenLock.WaitAsync(ct);
		try
		{
			if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
			{
				return _accessToken;
			}

			var tenantId = GetRequired("AzureAd:TenantId");
			var clientId = GetRequired("AzureAd:ClientId");
			var clientSecret = GetRequired("AzureAd:ClientSecret");
			var instance = _configuration["AzureAd:Instance"]?.Trim();
			if (string.IsNullOrWhiteSpace(instance))
			{
				instance = "https://login.microsoftonline.com/";
			}

			var scope = ResolveScope(clientId);
			var authority = BuildAuthority(instance, tenantId);

			var app = ConfidentialClientApplicationBuilder
				.Create(clientId)
				.WithClientSecret(clientSecret)
				.WithAuthority(authority)
				.Build();

			var result = await app
				.AcquireTokenForClient([scope])
				.ExecuteAsync(ct);

			_accessToken = result.AccessToken;
			_expiresAtUtc = result.ExpiresOn;

			_logger.LogDebug(
				"Acquired Azure AD access token for scope {Scope}, expires at {ExpiresAtUtc}.",
				scope,
				_expiresAtUtc);

			return _accessToken;
		}
		finally
		{
			_tokenLock.Release();
		}
	}

	private string ResolveScope(string clientId)
	{
		var configuredScope = _configuration["AzureAd:Scope"]?.Trim();
		if (!string.IsNullOrWhiteSpace(configuredScope))
		{
			return configuredScope;
		}

		var configuredApiClientId = _configuration["AzureAd:ApiClientId"]?.Trim();
		if (!string.IsNullOrWhiteSpace(configuredApiClientId))
		{
			return $"api://{configuredApiClientId}/.default";
		}

		_logger.LogWarning(
			"AzureAd:Scope and AzureAd:ApiClientId are not configured. Falling back to api://{ClientId}/.default.",
			clientId);
		return $"api://{clientId}/.default";
	}

	private static string BuildAuthority(string instance, string tenantId)
	{
		var normalized = instance.EndsWith('/') ? instance : instance + "/";
		return normalized + tenantId;
	}

	private string GetRequired(string key)
	{
		var value = _configuration[key];
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException($"Missing required configuration value '{key}'.");
		}

		return value;
	}
}
