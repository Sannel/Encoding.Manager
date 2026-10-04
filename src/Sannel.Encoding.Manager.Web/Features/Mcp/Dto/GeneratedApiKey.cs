namespace Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

/// <summary>A freshly generated API key. <see cref="Key"/> is returned once and never stored.</summary>
public class GeneratedApiKey
{
	public required string Key { get; init; }

	public required UserApiKeyInfo Info { get; init; }
}
