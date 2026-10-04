using Sannel.Encoding.Manager.Web.Features.Scan.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Services;

/// <summary>
/// Runs HandBrake disc scans in the background so callers never block for minutes,
/// and de-duplicates concurrent scans of the same disc.
/// </summary>
public interface IBackgroundScanCoordinator
{
	/// <summary>
	/// Starts a scan of the disc (or joins one already running) and waits up to <paramref name="maxWait"/>
	/// for it to finish. Returns the status at that point — Completed, Failed, or still Running.
	/// </summary>
	/// <exception cref="ArgumentException">The root label is unknown or the path escapes the root.</exception>
	Task<ScanJobStatus> StartOrGetAsync(string rootLabel, string relativePath, bool forceRescan, TimeSpan maxWait, CancellationToken ct = default);

	/// <summary>Returns the status of a scan started earlier, or null when none is known for the path.</summary>
	/// <exception cref="ArgumentException">The root label is unknown or the path escapes the root.</exception>
	ScanJobStatus? GetStatus(string rootLabel, string relativePath);
}
