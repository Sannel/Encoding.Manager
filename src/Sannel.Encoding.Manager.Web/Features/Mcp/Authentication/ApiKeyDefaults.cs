namespace Sannel.Encoding.Manager.Web.Features.Mcp.Authentication;

/// <summary>Names shared by the MCP API-key authentication scheme and policy.</summary>
public static class ApiKeyDefaults
{
	/// <summary>Authentication scheme name.</summary>
	public const string Scheme = "McpApiKey";

	/// <summary>Authorization policy applied to the MCP endpoint.</summary>
	public const string Policy = "McpApi";

	/// <summary>Prefix of every generated key.</summary>
	public const string KeyPrefix = "sem_";

	/// <summary>Alternative header for clients that cannot send an Authorization header.</summary>
	public const string HeaderName = "X-Api-Key";

	/// <summary>Claim type holding the authenticating key's id.</summary>
	public const string KeyIdClaim = "mcp_api_key_id";
}
