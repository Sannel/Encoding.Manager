using System.Security.Claims;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <summary>Creates, rotates, revokes and validates per-user MCP API keys.</summary>
public interface IUserApiKeyService
{
	/// <summary>Returns metadata for the user's key, or null when the user has none.</summary>
	Task<UserApiKeyInfo?> GetForUserAsync(string userObjectId, CancellationToken ct = default);

	/// <summary>Generates a new key for the user, replacing (and invalidating) any existing key.</summary>
	/// <exception cref="InvalidOperationException">The principal has no object id.</exception>
	Task<GeneratedApiKey> CreateOrRotateAsync(ClaimsPrincipal user, CancellationToken ct = default);

	/// <summary>Deletes the user's key. Returns false when the user had none.</summary>
	Task<bool> RevokeAsync(string userObjectId, CancellationToken ct = default);

	/// <summary>Finds the key record matching a raw key, or null.</summary>
	Task<UserApiKey?> FindByKeyAsync(string rawKey, CancellationToken ct = default);

	/// <summary>Records a successful use of the key (throttled to once per minute).</summary>
	Task TouchLastUsedAsync(UserApiKey key, CancellationToken ct = default);
}
