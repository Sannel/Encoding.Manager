using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Sannel.Encoding.Manager.Web.Features.Shared.Services;

/// <summary>Extracts the stable identity fields used for auditing from an Entra ID principal.</summary>
public static class UserIdentity
{
	/// <summary>Entra object id (<c>oid</c>), or null when the principal has none.</summary>
	public static string? GetObjectId(ClaimsPrincipal? principal) =>
		principal is null ? null : ClaimsPrincipalExtensions.GetObjectId(principal);

	/// <summary>Display name: <c>name</c>, falling back to <c>preferred_username</c> and the identity name.</summary>
	public static string? GetDisplayName(ClaimsPrincipal? principal) =>
		principal?.FindFirst("name")?.Value
		?? principal?.FindFirst("preferred_username")?.Value
		?? principal?.Identity?.Name;

	/// <summary>User principal name (<c>preferred_username</c>), or null.</summary>
	public static string? GetPrincipalName(ClaimsPrincipal? principal) =>
		principal?.FindFirst("preferred_username")?.Value
		?? principal?.FindFirst(ClaimTypes.Upn)?.Value;
}
