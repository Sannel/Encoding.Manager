using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>
/// Interlace verdicts for disc titles and media files. Never blocks on ffmpeg: uncached sources are probed in the
/// background (deduplicated, limited concurrency) and reported as <see cref="InterlaceVerdict.Pending"/> until done.
/// </summary>
public interface IInterlaceService
{
	/// <summary>
	/// Verdicts for a scanned disc's titles, keyed by HandBrake title number. DVD titles are always interlaced;
	/// Blu-ray titles combine HandBrake's flag with an ffmpeg probe of their playlist.
	/// </summary>
	Task<IReadOnlyDictionary<int, InterlaceResult>> GetDiscTitlesAsync(string discPhysicalPath, IReadOnlyList<TitleInfo> titles, CancellationToken ct = default);

	/// <summary>Verdicts for media files, keyed by physical path.</summary>
	Task<IReadOnlyDictionary<string, InterlaceResult>> GetFilesAsync(IReadOnlyList<string> filePhysicalPaths, CancellationToken ct = default);
}
