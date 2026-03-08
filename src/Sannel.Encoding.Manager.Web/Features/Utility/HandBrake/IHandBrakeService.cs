namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>
/// Service for interacting with HandBrakeCLI to scan and encode media files.
/// </summary>
public interface IHandBrakeService
{
	/// <summary>Scans the input file and returns track/title/stream metadata.</summary>
	Task<HandBrakeScanResult> ScanAsync(string inputPath, CancellationToken ct = default);

	/// <summary>Encodes the input according to the job spec, reporting progress via <paramref name="progress"/>.</summary>
	Task<HandBrakeEncodeResult> EncodeAsync(
		HandBrakeJob job,
		IProgress<ProgressInfo>? progress = null,
		CancellationToken ct = default);

	/// <summary>Returns the detected HandBrakeCLI version string.</summary>
	string CliVersion { get; }
}
