namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>Displayable metadata about a user's API key. Never contains the key itself.</summary>
public class UserApiKeyInfo
{
	public required string KeyPrefix { get; init; }

	public required DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset? LastUsedAt { get; init; }
}
