using System.Security.Claims;
using Sannel.Encoding.Manager.Web.Features.Mcp.Authentication;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <summary>The authenticated MCP caller for the current request.</summary>
public class McpCaller
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public McpCaller(IHttpContextAccessor httpContextAccessor) =>
		this._httpContextAccessor = httpContextAccessor;

	public ClaimsPrincipal? User => this._httpContextAccessor.HttpContext?.User;

	/// <summary>Stable id used for per-caller limits: the API key id, else the user's object id.</summary>
	public string CallerId =>
		this.User?.FindFirst(ApiKeyDefaults.KeyIdClaim)?.Value
		?? UserIdentity.GetObjectId(this.User)
		?? "anonymous";

	public string? DisplayName => UserIdentity.GetDisplayName(this.User);

	public string? ObjectId => UserIdentity.GetObjectId(this.User);
}
