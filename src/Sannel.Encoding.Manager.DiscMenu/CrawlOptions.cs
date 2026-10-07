namespace Sannel.Encoding.Manager.DiscMenu;

/// <summary>Limits and preferences for a menu crawl.</summary>
public class CrawlOptions
{
	/// <summary>Maximum number of distinct menus to record.</summary>
	public int MaxMenus { get; set; } = 64;

	/// <summary>Maximum number of button activations to try.</summary>
	public int MaxActions { get; set; } = 500;

	/// <summary>Wall-clock budget for the whole crawl.</summary>
	public TimeSpan TimeBudget { get; set; } = TimeSpan.FromSeconds(240);

	/// <summary>Preferred menu language (ISO 639-1, e.g. "en").</summary>
	public string MenuLanguage { get; set; } = "en";

	/// <summary>
	/// Folder for BD-J graphics planes ({menuId}-s{button}-overlay.png, one per focus state), which the screenshot
	/// renderer draws over the menu's video because libvlc cannot blend BD-J graphics itself. Null = not saved.
	/// </summary>
	public string? OverlayFolder { get; set; }

	/// <summary>Maximum blocks/events read while waiting for one navigation step to settle.</summary>
	public int MaxStepsPerRun { get; set; } = 200_000;
}
