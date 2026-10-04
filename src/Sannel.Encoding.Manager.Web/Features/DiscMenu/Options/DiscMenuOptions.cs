namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Options;

/// <summary>Configuration for disc menu inspection (config section <c>DiscMenu</c>).</summary>
public class DiscMenuOptions
{
	public bool Enabled { get; set; } = true;

	/// <summary>JRE root used by libbluray for BD-J (Java) menus. Null = BD-J menus are not crawled.</summary>
	public string? JavaHome { get; set; }

	/// <summary>Path to libbluray-j2se-*.jar. Null = auto-detect (/usr/share/java on Linux).</summary>
	public string? LibBlurayJarPath { get; set; }

	/// <summary>Folder containing libdvdnav / libdvdread / libbluray (required on Windows).</summary>
	public string? NativeLibraryPath { get; set; }

	/// <summary>Probe assembly (.dll) or executable. Null = the copy published next to the web app.</summary>
	public string? ProbePath { get; set; }

	/// <summary>Screenshot root, relative to the content root unless absolute.</summary>
	public string OutputPath { get; set; } = "disc-menus";

	public int ProbeTimeoutSeconds { get; set; } = 300;

	public int MaxConcurrentProbes { get; set; } = 1;

	public int MaxMenus { get; set; } = 64;

	public int MaxActions { get; set; } = 500;

	public int TimeBudgetSeconds { get; set; } = 240;

	public string MenuLanguage { get; set; } = "en";

	public int ScreenshotWidth { get; set; } = 960;

	public int SettleMilliseconds { get; set; } = 1500;

	/// <summary>Set by the host: the application content root.</summary>
	public string ContentRootPath { get; set; } = string.Empty;
}
