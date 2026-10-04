using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;

/// <summary>Outcome of one probe process run.</summary>
public class DiscMenuProbeResult
{
	public DiscMenuMap? Map { get; init; }

	/// <summary>True when the probe reported that it cannot run on this host (exit code 2).</summary>
	public bool Unavailable { get; init; }

	public string? Error { get; init; }
}
