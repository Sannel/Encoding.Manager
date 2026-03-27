using System.Runtime.InteropServices;

namespace Sannel.Encoding.Runner.Features.Runner.Services;

/// <summary>
/// Converts DB-stored forward-slash paths to native OS paths on the runner side.
/// </summary>
public class PathNormalizer
{
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
		Path.Combine(localRootPath, ToNative(forwardSlashRelativePath));

	/// <summary>
	/// Expands a forward-slash template with variable substitutions and combines with a local root.
	/// </summary>
	public string ExpandTemplate(string forwardSlashTemplate, IDictionary<string, string> variables, string localRootPath)
	{
		var expanded = forwardSlashTemplate;
		foreach (var kvp in variables)
		{
			expanded = expanded.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
		}

		return Path.Combine(localRootPath, ToNative(expanded));
	}
}
