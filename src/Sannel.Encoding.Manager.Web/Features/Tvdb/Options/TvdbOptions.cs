namespace Sannel.Encoding.Manager.Web.Features.Tvdb.Options;

/// <summary>
/// Configuration options for TheTVDB API integration.
/// Bound from the "Tvdb" section in appsettings.json.
/// Store the ApiKey in user-secrets or environment variables — never in source control.
/// </summary>
public class TvdbOptions
{
	/// <summary>
	/// TheTVDB v4 API base URL.
	/// </summary>
	public string BaseUrl { get; set; } = "https://api4.thetvdb.com/v4";

	/// <summary>
	/// TheTVDB API key. Obtain from https://thetvdb.com/dashboard/account/apikey
	/// </summary>
	public string ApiKey { get; set; } = string.Empty;
}
