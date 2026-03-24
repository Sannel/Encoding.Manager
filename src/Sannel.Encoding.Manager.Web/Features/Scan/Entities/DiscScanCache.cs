namespace Sannel.Encoding.Manager.Web.Features.Scan.Entities;

public class DiscScanCache
{
	/// <summary>The full physical input path used as the cache key.</summary>
	public string InputPath { get; set; } = string.Empty;

	/// <summary>The raw JSON stdout from HandBrakeCLI --json --scan.</summary>
	public string ScanJson { get; set; } = string.Empty;

	public DateTimeOffset CachedAt { get; set; }
}
