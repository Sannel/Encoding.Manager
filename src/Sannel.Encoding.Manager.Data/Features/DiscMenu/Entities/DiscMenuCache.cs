namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Entities;

/// <summary>Cached result of a disc menu inspection (probe run) for one disc.</summary>
public class DiscMenuCache
{
	/// <summary>The full physical disc path used as the cache key.</summary>
	public string InputPath { get; set; } = string.Empty;

	/// <summary>"DVD" or "BluRay".</summary>
	public string DiscType { get; set; } = string.Empty;

	/// <summary>The serialized disc menu map returned by the probe (with HandBrake titles filled in).</summary>
	public string MenuJson { get; set; } = string.Empty;

	/// <summary>Absolute folder holding the menu screenshots for this disc.</summary>
	public string ScreenshotFolder { get; set; } = string.Empty;

	/// <summary>Probe model version that produced this row. Older versions are treated as a cache miss.</summary>
	public int ProbeVersion { get; set; }

	public DateTimeOffset CachedAt { get; set; }
}
