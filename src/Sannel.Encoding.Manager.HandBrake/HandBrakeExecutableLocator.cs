using System.Runtime.InteropServices;

namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Locates the HandBrakeCLI executable on the current platform.
/// Supports direct native installs and Flatpak (Linux).
/// </summary>
public static class HandBrakeExecutableLocator
{
	private const string FlatpakAppId = "fr.handbrake.ghb";

	/// <summary>
	/// Resolves the HandBrakeCLI executable. Uses <paramref name="configuredPath"/> if non-empty,
	/// otherwise auto-detects based on the current OS.
	/// </summary>
	/// <param name="configuredPath">Value from <see cref="HandBrakeOptions.ExecutablePath"/>.</param>
	/// <param name="processRunner">Used to probe Flatpak on Linux.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A <see cref="HandBrakeExecutable"/> describing how to launch HandBrakeCLI.</returns>
	/// <exception cref="InvalidOperationException">Thrown if HandBrakeCLI cannot be found.</exception>
	public static async Task<HandBrakeExecutable> LocateAsync(
		string configuredPath,
		IProcessRunner processRunner,
		CancellationToken ct = default)
	{
		// 1. Use configured path if provided
		if (!string.IsNullOrWhiteSpace(configuredPath))
		{
			if (File.Exists(configuredPath))
			{
				return new HandBrakeExecutable(configuredPath, []);
			}

			throw new InvalidOperationException(
				$"HandBrakeCLI executable not found at configured path: {configuredPath}");
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return LocateOnWindows();
		}

		// Linux
		return await LocateOnLinuxAsync(processRunner, ct).ConfigureAwait(false);
	}

	private static HandBrakeExecutable LocateOnWindows()
	{
		var exeName = "HandBrakeCLI.exe";

		// Search PATH
		var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
		foreach (var dir in pathDirs)
		{
			var candidate = Path.Combine(dir, exeName);
			if (File.Exists(candidate))
			{
				return new HandBrakeExecutable(candidate, []);
			}
		}

		// Fallback: Program Files
		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var fallback = Path.Combine(programFiles, "HandBrake", exeName);
		if (File.Exists(fallback))
		{
			return new HandBrakeExecutable(fallback, []);
		}

		throw new InvalidOperationException(
			$"HandBrakeCLI.exe not found in PATH or {fallback}. Please install HandBrake or set HandBrake:ExecutablePath in appsettings.json.");
	}

	private static async Task<HandBrakeExecutable> LocateOnLinuxAsync(
		IProcessRunner processRunner,
		CancellationToken ct)
	{
		var exeName = "HandBrakeCLI";

		// Search PATH
		var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
		foreach (var dir in pathDirs)
		{
			var candidate = Path.Combine(dir, exeName);
			if (File.Exists(candidate))
			{
				return new HandBrakeExecutable(candidate, []);
			}
		}

		// Fallback: /usr/bin
		var fallback = Path.Combine("/usr/bin", exeName);
		if (File.Exists(fallback))
		{
			return new HandBrakeExecutable(fallback, []);
		}

		// Probe Flatpak
		if (await IsFlatpakInstalledAsync(processRunner, ct).ConfigureAwait(false))
		{
			return new HandBrakeExecutable("flatpak", ["run", "--command=HandBrakeCLI", FlatpakAppId]);
		}

		throw new InvalidOperationException(
			$"HandBrakeCLI not found in PATH, /usr/bin, or as a Flatpak app. Please install HandBrake or set HandBrake:ExecutablePath in appsettings.json.");
	}

	private static async Task<bool> IsFlatpakInstalledAsync(
		IProcessRunner processRunner,
		CancellationToken ct)
	{
		try
		{
			var result = await processRunner.RunAsync(
				"flatpak",
				["list", "--app", "--columns=application"],
				ct).ConfigureAwait(false);

			return result.ExitCode == 0
				&& result.StandardOutput.Contains(FlatpakAppId, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			// flatpak command not available
			return false;
		}
	}
}
