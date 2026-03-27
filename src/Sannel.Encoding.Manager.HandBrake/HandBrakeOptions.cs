namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Configuration options for HandBrake CLI integration.
/// Bound from the "HandBrake" section in appsettings.json.
/// </summary>
public class HandBrakeOptions
{
	/// <summary>
	/// Path to the HandBrakeCLI executable. Leave empty to auto-detect from PATH.
	/// </summary>
	public string ExecutablePath { get; set; } = string.Empty;

	/// <summary>
	/// Directory where scan result JSON files are written.
	/// Relative paths are resolved relative to the app's content root.
	/// </summary>
	public string ScanOutputPath { get; set; } = "handbrake-scans";

	/// <summary>
	/// Minimum acceptable HandBrakeCLI version. Parsed as System.Version.
	/// </summary>
	public string MinimumVersion { get; set; } = "10.1";

	/// <summary>
	/// Application content root path used to resolve relative <see cref="ScanOutputPath"/>.
	/// Set during DI registration (e.g. from <c>IWebHostEnvironment.ContentRootPath</c>).
	/// </summary>
	public string ContentRootPath { get; set; } = string.Empty;
}
