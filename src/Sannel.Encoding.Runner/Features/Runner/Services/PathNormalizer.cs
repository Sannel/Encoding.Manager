using System.Runtime.InteropServices;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

/// <summary>
/// Converts DB-stored forward-slash paths to native OS paths on the runner side.
/// </summary>
public class PathNormalizer
{
	// Union of the current OS's invalid filename chars and the Windows-specific set so that
	// output paths are safe on all platforms (Windows forbids :, *, ?, ", <, >, |, \, /).
	private static readonly HashSet<char> _invalidFileNameChars =
		new(Path.GetInvalidFileNameChars()
			.Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|']));

	/// <summary>
	/// Replaces any character that is invalid in a file name segment with an underscore.
	/// Path separators are included so variable values cannot introduce extra path levels.
	/// </summary>
	public string SanitizeSegment(string value) =>
		string.Concat(value.Select(c => _invalidFileNameChars.Contains(c) ? '_' : c));

	/// <summary>
	/// Converts forward slashes to native OS directory separators.
	/// No-op on Linux; replaces / with \ on Windows.
	/// </summary>
	public string ToNative(string forwardSlashPath) =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? forwardSlashPath.Replace('/', '\\')
			: forwardSlashPath;

	/// <summary>
	/// Combines a local root path with a forward-slash relative path, converting to native separators.
	/// </summary>
	public string CombineWithRoot(string localRootPath, string forwardSlashRelativePath) =>
		Path.Combine(localRootPath, this.ToNative(forwardSlashRelativePath));

	/// <summary>
	/// Expands a forward-slash template with variable substitutions and combines with a local root.
	/// Each variable value is sanitized before substitution so that invalid file-name characters
	/// (e.g. ':' in "Doctor Who: The Movie") are replaced with '_'.
	/// </summary>
	public string ExpandTemplate(string forwardSlashTemplate, IDictionary<string, string> variables, string localRootPath)
	{
		var expanded = forwardSlashTemplate;
		foreach (var kvp in variables)
		{
			expanded = expanded.Replace(kvp.Key, this.SanitizeSegment(kvp.Value), StringComparison.OrdinalIgnoreCase);
		}

		return Path.Combine(localRootPath, this.ToNative(expanded));
	}
}
