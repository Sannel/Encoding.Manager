using Sannel.Encoding.Manager.Web.Features.Settings.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Settings.Services;

public interface ISettingsService
{
	/// <summary>
	/// Returns the current application settings.
	/// Returns a default instance if no settings have been saved yet.
	/// </summary>
	Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);

	/// <summary>Persists the supplied settings, replacing any existing row.</summary>
	Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
}
