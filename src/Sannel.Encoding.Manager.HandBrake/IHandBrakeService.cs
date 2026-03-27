namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Service for interacting with HandBrakeCLI to scan and encode media files.
/// </summary>
public interface IHandBrakeService
{
	/// <summary>Scans the input file and returns track/title/stream metadata.</summary>
	/// <param name="inputPath">Path to the disc image or directory to scan.</param>
	/// <param name="forceRescan">When true, skip the 24-hour disc scan cache and always call HandBrakeCLI.</param>
	Task<HandBrakeScanResult> ScanAsync(string inputPath, bool forceRescan = false, CancellationToken ct = default);

	/// <summary>Encodes the input according to the job spec, reporting progress via <paramref name="progress"/>.</summary>
	Task<HandBrakeEncodeResult> EncodeAsync(
		HandBrakeJob job,
		IProgress<ProgressInfo>? progress = null,
		CancellationToken ct = default);

	/// <summary>Returns the detected HandBrakeCLI version string.</summary>
	string CliVersion { get; }
}
