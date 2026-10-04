using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Mcp.Authentication;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Entities;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <inheritdoc />
public class UserApiKeyService : IUserApiKeyService
{
	private const int _prefixLength = 8;
	private static readonly TimeSpan _touchInterval = TimeSpan.FromMinutes(1);

	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public UserApiKeyService(IDbContextFactory<AppDbContext> dbFactory) =>
		this._dbFactory = dbFactory;

	/// <inheritdoc />
	public async Task<UserApiKeyInfo?> GetForUserAsync(string userObjectId, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var key = await ctx.UserApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserObjectId == userObjectId, ct);
		return key is null ? null : ToInfo(key);
	}

	/// <inheritdoc />
	public async Task<GeneratedApiKey> CreateOrRotateAsync(ClaimsPrincipal user, CancellationToken ct = default)
	{
		var objectId = UserIdentity.GetObjectId(user)
			?? throw new InvalidOperationException("The signed-in user has no object id (oid) claim.");

		var rawKey = ApiKeyDefaults.KeyPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var entity = await ctx.UserApiKeys.FirstOrDefaultAsync(k => k.UserObjectId == objectId, ct);
		if (entity is null)
		{
			entity = new UserApiKey { UserObjectId = objectId };
			ctx.UserApiKeys.Add(entity);
		}

		entity.UserDisplayName = UserIdentity.GetDisplayName(user);
		entity.UserPrincipalName = UserIdentity.GetPrincipalName(user);
		entity.KeyHash = Hash(rawKey);
		entity.KeyPrefix = rawKey[.._prefixLength];
		entity.CreatedAt = DateTimeOffset.UtcNow;
		entity.LastUsedAt = null;
		await ctx.SaveChangesAsync(ct);

		return new GeneratedApiKey { Key = rawKey, Info = ToInfo(entity) };
	}

	/// <inheritdoc />
	public async Task<bool> RevokeAsync(string userObjectId, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var deleted = await ctx.UserApiKeys.Where(k => k.UserObjectId == userObjectId).ExecuteDeleteAsync(ct);
		return deleted > 0;
	}

	/// <inheritdoc />
	public async Task<UserApiKey?> FindByKeyAsync(string rawKey, CancellationToken ct = default)
	{
		if (!rawKey.StartsWith(ApiKeyDefaults.KeyPrefix, StringComparison.Ordinal))
		{
			return null;
		}

		var hash = Hash(rawKey);
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var key = await ctx.UserApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyHash == hash, ct);
		if (key is null)
		{
			return null;
		}

		// The lookup already matched; compare in constant time anyway so timing never depends on the key.
		return CryptographicOperations.FixedTimeEquals(
			Convert.FromHexString(key.KeyHash),
			Convert.FromHexString(hash)) ? key : null;
	}

	/// <inheritdoc />
	public async Task TouchLastUsedAsync(UserApiKey key, CancellationToken ct = default)
	{
		var now = DateTimeOffset.UtcNow;
		if (key.LastUsedAt is { } last && now - last < _touchInterval)
		{
			return;
		}

		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var entity = await ctx.UserApiKeys.FirstOrDefaultAsync(k => k.Id == key.Id, ct);
		if (entity is not null)
		{
			entity.LastUsedAt = now;
			await ctx.SaveChangesAsync(ct);
		}
	}

	internal static string Hash(string rawKey) =>
		Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawKey)));

	private static UserApiKeyInfo ToInfo(UserApiKey key) => new()
	{
		KeyPrefix = key.KeyPrefix,
		CreatedAt = key.CreatedAt,
		LastUsedAt = key.LastUsedAt,
	};
}
