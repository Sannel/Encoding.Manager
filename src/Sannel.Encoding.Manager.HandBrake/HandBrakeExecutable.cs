namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Resolved HandBrake executable information.
/// </summary>
/// <param name="Binary">The executable to launch (e.g. "HandBrakeCLI" or "flatpak").</param>
/// <param name="PrefixArgs">Arguments to prepend before HandBrake flags (e.g. ["run", "--command=HandBrakeCLI", "fr.handbrake.HandBrake"] for Flatpak).</param>
public record HandBrakeExecutable(string Binary, IReadOnlyList<string> PrefixArgs);
