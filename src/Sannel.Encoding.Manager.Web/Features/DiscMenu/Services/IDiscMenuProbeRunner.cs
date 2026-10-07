using Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

/// <summary>Runs the out-of-process disc menu probe.</summary>
public interface IDiscMenuProbeRunner
{
	/// <summary>Runs the probe for one disc, writing screenshots to <paramref name="screenshotFolder"/>.</summary>
	/// <param name="discType">"dvd" or "bluray".</param>
	Task<DiscMenuProbeResult> RunAsync(string physicalPath, string discType, string screenshotFolder, CancellationToken ct);
}
