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

	/// <summary>Maximum blocks/events read while waiting for one navigation step to settle.</summary>
	public int MaxStepsPerRun { get; set; } = 200_000;
}
