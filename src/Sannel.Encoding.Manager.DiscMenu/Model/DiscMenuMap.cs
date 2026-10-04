namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>Everything learned about a disc's menus. Produced by the probe, consumed by the web app and the AI.</summary>
public class DiscMenuMap
{
	/// <summary>Bump when the crawl logic or schema changes so cached maps are re-probed.</summary>
	public const int CurrentProbeVersion = 1;

	/// <summary>"DVD" or "BluRay".</summary>
	public string DiscType { get; set; } = string.Empty;

	public int ProbeVersion { get; set; } = CurrentProbeVersion;

	/// <summary>"DVD", "HDMV", "BD-J" or "None".</summary>
	public string MenuSystem { get; set; } = "None";

	/// <summary>False when a crawl limit was hit or part of the disc could not be explored.</summary>
	public bool Complete { get; set; } = true;

	public List<string> Warnings { get; set; } = [];

	/// <summary>What the disc does on insert, before any menu.</summary>
	public ButtonAction? FirstPlay { get; set; }

	public List<MenuNode> Menus { get; set; } = [];

	public List<DiscTitle> Titles { get; set; } = [];

	/// <summary>How long the crawl took.</summary>
	public int CrawlMilliseconds { get; set; }
}
