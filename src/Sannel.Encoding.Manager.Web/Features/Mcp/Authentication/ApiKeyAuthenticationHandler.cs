using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Authentication;

/// <summary>
/// Authenticates MCP requests with a per-user API key sent as <c>Authorization: Bearer sem_…</c>
/// or <c>X-Api-Key: sem_…</c>.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
	public ApiKeyAuthenticationHandler(
		IOptionsMonitor<ApiKeyAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: base(options, logger, encoder)
	{
	}

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var key = this.ReadKey();
		if (key is null)
		{
			return AuthenticateResult.NoResult();
		}

		var keyService = this.Context.RequestServices.GetRequiredService<IUserApiKeyService>();
		var record = await keyService.FindByKeyAsync(key, this.Context.RequestAborted);
		if (record is null)
		{
			this.Logger.LogWarning("Rejected MCP request with unknown API key {KeyPrefix}", SafePrefix(key));
			return AuthenticateResult.Fail("Invalid API key.");
		}

		await keyService.TouchLastUsedAsync(record, this.Context.RequestAborted);

		var claims = new List<Claim>
		{
			new("oid", record.UserObjectId),
			new("http://schemas.microsoft.com/identity/claims/objectidentifier", record.UserObjectId),
			new(ApiKeyDefaults.KeyIdClaim, record.Id.ToString()),
			new("auth_method", "mcp_api_key"),
		};
		if (!string.IsNullOrEmpty(record.UserDisplayName))
		{
			claims.Add(new Claim("name", record.UserDisplayName));
		}

		if (!string.IsNullOrEmpty(record.UserPrincipalName))
		{
			claims.Add(new Claim("preferred_username", record.UserPrincipalName));
		}

		var identity = new ClaimsIdentity(claims, this.Scheme.Name, "name", ClaimTypes.Role);
		return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), this.Scheme.Name));
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		this.Response.StatusCode = StatusCodes.Status401Unauthorized;
		this.Response.Headers.WWWAuthenticate = "Bearer realm=\"mcp\"";
		return Task.CompletedTask;
	}

	private string? ReadKey()
	{
		var authorization = this.Request.Headers.Authorization.ToString();
		if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
		{
			var token = authorization["Bearer ".Length..].Trim();
			if (token.StartsWith(ApiKeyDefaults.KeyPrefix, StringComparison.Ordinal))
			{
				return token;
			}
		}

		var header = this.Request.Headers[ApiKeyDefaults.HeaderName].ToString().Trim();
		return header.StartsWith(ApiKeyDefaults.KeyPrefix, StringComparison.Ordinal) ? header : null;
	}

	private static string SafePrefix(string key) => key.Length > 8 ? key[..8] : key;
}
