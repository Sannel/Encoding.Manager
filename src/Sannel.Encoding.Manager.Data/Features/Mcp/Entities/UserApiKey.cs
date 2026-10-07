namespace Sannel.Encoding.Manager.Web.Features.Mcp.Entities;

/// <summary>
/// A per-user API key used to authenticate MCP clients. Only a SHA-256 hash of the key is stored;
/// the plaintext is shown to the user once when generated and never persisted.
/// </summary>
public class UserApiKey
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Entra ID object id (<c>oid</c> claim) of the key owner. Unique — one key per user.</summary>
	public string UserObjectId { get; set; } = string.Empty;

	/// <summary>Display name (<c>name</c> claim) of the key owner.</summary>
	public string? UserDisplayName { get; set; }

	/// <summary>User principal name (<c>preferred_username</c> claim) of the key owner.</summary>
	public string? UserPrincipalName { get; set; }

	/// <summary>Lower-case hex SHA-256 of the full key. Unique; used for lookup during authentication.</summary>
	public string KeyHash { get; set; } = string.Empty;

	/// <summary>First characters of the key (e.g. <c>sem_Ab12</c>) for display and logs.</summary>
	public string KeyPrefix { get; set; } = string.Empty;

	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>Last successful authentication with this key (updated at most once per minute).</summary>
	public DateTimeOffset? LastUsedAt { get; set; }
}
