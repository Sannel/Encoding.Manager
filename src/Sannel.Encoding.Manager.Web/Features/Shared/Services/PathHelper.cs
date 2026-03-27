namespace Sannel.Encoding.Manager.Web.Features.Shared.Services;

/// <summary>
/// Path normalization helpers for database storage.
/// All paths stored in the database use forward slashes (Linux-style).
/// </summary>
public static class PathHelper
{
	/// <summary>
	/// Replaces all backslashes with forward slashes for consistent DB storage.
	/// </summary>
	public static string ToForwardSlash(string path) =>
		path.Replace('\\', '/');
}
