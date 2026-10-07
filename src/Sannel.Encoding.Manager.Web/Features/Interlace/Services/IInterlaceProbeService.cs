using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>Runs ffmpeg's <c>idet</c> filter on a few samples of a Blu-ray title or a media file.</summary>
public interface IInterlaceProbeService
{
	/// <summary>Probes one Blu-ray playlist (ffmpeg <c>bluray:</c> protocol). Null when ffmpeg is unavailable or failed.</summary>
	Task<InterlaceMeasurement?> ProbeBlurayTitleAsync(string discPath, int playlist, TimeSpan duration, CancellationToken ct = default);

	/// <summary>Probes one media file. Null when ffmpeg is unavailable or failed.</summary>
	Task<InterlaceMeasurement?> ProbeFileAsync(string filePath, CancellationToken ct = default);
}
