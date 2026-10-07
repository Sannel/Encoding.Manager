using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

/// <summary>Inspects disc menus in the background (via the probe process) and caches the results.</summary>
public interface IDiscMenuService
{
	/// <summary>
	/// Returns the cached menu map, joins a running inspection, or starts one — then waits up to
	/// <paramref name="maxWait"/> for it to finish.
	/// </summary>
	/// <exception cref="ArgumentException">Unknown root, a path outside the root, or not a DVD / Blu-ray folder.</exception>
	Task<DiscMenuJobStatus> StartOrGetAsync(string rootLabel, string relativePath, bool forceRefresh, TimeSpan maxWait, CancellationToken ct = default);

	/// <summary>Status of the disc's inspection (running, finished, or cached), or null when it was never inspected.</summary>
	/// <exception cref="ArgumentException">Unknown root or a path outside the root.</exception>
	Task<DiscMenuJobStatus?> GetStatusAsync(string rootLabel, string relativePath, CancellationToken ct = default);

	/// <summary>
	/// The PNG of a menu screenshot (or, with <paramref name="buttonNumber"/>, of the menu with that button
	/// highlighted) and its menu, or null when not available.
	/// </summary>
	Task<(byte[] Png, MenuNode Menu)?> GetScreenshotAsync(string rootLabel, string relativePath, string menuId, bool annotated, int? buttonNumber = null, CancellationToken ct = default);
}
