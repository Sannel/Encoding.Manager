using Sannel.Encoding.Manager.DiscMenu;
using Sannel.Encoding.Manager.DiscMenu.Bluray;
using Sannel.Encoding.Manager.DiscMenu.Dvd;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.DiscMenu.Native;
using Sannel.Encoding.Manager.DiscMenu.Probe;
using Sannel.Encoding.Manager.DiscMenu.Probe.Rendering;

// Disc menu probe: crawls one disc's menus in an isolated process and writes a DiscMenuMap as JSON to --json.
// Exit codes: 0 = map written, 1 = failure, 2 = unsupported disc or native library missing.
var startedUtc = DateTime.UtcNow;
ProbeArguments arguments;
try
{
	arguments = ProbeArguments.Parse(args);
}
catch (ArgumentException ex)
{
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine(ProbeArguments.Usage);
	return 1;
}

void Log(string message) => Console.Error.WriteLine($"[probe] {message}");

NativeLibraryResolver.Install(arguments.NativePath);
var library = arguments.DiscType == "dvd" ? "dvdnav" : "bluray";
if (NativeLibraryResolver.Probe(library) is { } missing)
{
	Console.Error.WriteLine(missing);
	return 2;
}

var options = new CrawlOptions
{
	MaxMenus = arguments.MaxMenus,
	MaxActions = arguments.MaxActions,
	TimeBudget = TimeSpan.FromSeconds(arguments.TimeBudgetSeconds),
	MenuLanguage = arguments.MenuLanguage,
};

DiscMenuMap map;
try
{
	map = arguments.DiscType == "dvd"
		? new DvdMenuCrawler(options, Log).Crawl(arguments.Input)
		: new BlurayMenuCrawler(options, Log).Crawl(arguments.Input);
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Menu crawl failed: {ex}");
	return 1;
}

if (arguments.Screenshots && map.Menus.Count > 0)
{
	try
	{
		Directory.CreateDirectory(arguments.OutputFolder);
		using var renderer = new MenuScreenshotRenderer(
			arguments.ScreenshotWidth,
			TimeSpan.FromMilliseconds(arguments.SettleMilliseconds),
			startedUtc.AddSeconds(arguments.MaxSeconds),
			Log);
		renderer.Render(arguments.Input, arguments.DiscType, map, arguments.OutputFolder);
	}
	catch (Exception ex)
	{
		map.Warnings.Add($"Screenshots could not be rendered: {ex.Message}");
		Log($"screenshot rendering failed: {ex}");
	}
}

// The map goes to a file: libdvdread / libbluray print diagnostics on stdout.
File.WriteAllText(arguments.JsonPath, DiscMenuJson.Serialize(map));
return 0;
