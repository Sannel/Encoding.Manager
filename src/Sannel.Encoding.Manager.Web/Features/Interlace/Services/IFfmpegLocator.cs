using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>Finds the configured ffmpeg and checks it (version, Blu-ray support). The result is cached.</summary>
public interface IFfmpegLocator
{
	Task<FfmpegInfo> GetAsync(CancellationToken ct = default);
}
