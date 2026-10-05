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

void Log(string message) => Console.Error.WriteLine($"[probe] {(DateTime.UtcNow - startedUtc).TotalSeconds,6:0.0}s {message}");

NativeLibraryResolver.Install(arguments.NativePath);
var library = arguments.DiscType == "dvd" ? "dvdnav" : "bluray";
if (NativeLibraryResolver.Probe(library) is { } missing)
{
	Console.Error.WriteLine(missing);
	return 2;
}

var deadline = startedUtc.AddSeconds(arguments.MaxSeconds);

if (arguments.RenderOnly)
{
	if (DiscMenuJson.Deserialize(File.ReadAllText(arguments.JsonPath)) is not { } toRender)
	{
		Console.Error.WriteLine("--render-only: the map in --json could not be read.");
		return 1;
	}

	RenderInProcess(toRender);
	WriteMap(toRender);
	Environment.Exit(0);
}

var options = new CrawlOptions
{
	MaxMenus = arguments.MaxMenus,
	MaxActions = arguments.MaxActions,
	TimeBudget = TimeSpan.FromSeconds(arguments.TimeBudgetSeconds),
	MenuLanguage = arguments.MenuLanguage,
	OverlayFolder = arguments.Screenshots ? arguments.OutputFolder : null,
};

if (arguments.CrawlOnly)
{
	// Child of a Blu-ray probe: crawl in this process, saving the map after every step so the parent keeps it even
	// if this process has to be killed.
	try
	{
		var crawler = new BlurayMenuCrawler(options, Log) { Progress = WriteMap };
		WriteMap(crawler.Crawl(arguments.Input));
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"Menu crawl failed: {ex}");
		Environment.Exit(1);
	}

	Environment.Exit(0);
}

DiscMenuMap map;
try
{
	if (arguments.DiscType == "dvd")
	{
		map = new DvdMenuCrawler(options, Log).Crawl(arguments.Input);
	}
	else if (CrawlInChild() is { } crawled)
	{
		map = crawled;
	}
	else
	{
		return 1;
	}
}
catch (Exception ex)
{
	Console.Error.WriteLine($"Menu crawl failed: {ex}");
	return 1;
}

if (arguments.Screenshots && map.Menus.Count > 0)
{
	if (arguments.DiscType == "dvd")
	{
		RenderInProcess(map);
	}
	else
	{
		map = RenderInChild(map);
	}
}

// The map goes to a file: libdvdread / libbluray print diagnostics on stdout. Exit hard: a crawl thread stuck in
// native code would otherwise keep the process alive.
WriteMap(map);
Environment.Exit(0);
return 0;

void RenderInProcess(DiscMenuMap target)
{
	try
	{
		Directory.CreateDirectory(arguments.OutputFolder);
		using var renderer = new MenuScreenshotRenderer(
			arguments.ScreenshotWidth,
			TimeSpan.FromMilliseconds(arguments.SettleMilliseconds),
			deadline,
			Log);
		renderer.Render(arguments.Input, arguments.DiscType, target, arguments.OutputFolder);
	}
	catch (Exception ex)
	{
		target.Warnings.Add($"Screenshots could not be rendered: {ex.Message}");
		Log($"screenshot rendering failed: {ex}");
	}
}

// Writes the map atomically (a killed child must never leave half a file behind).
void WriteMap(DiscMenuMap value)
{
	var temporary = arguments.JsonPath + ".tmp";
	File.WriteAllText(temporary, DiscMenuJson.Serialize(value));
	File.Move(temporary, arguments.JsonPath, overwrite: true);
}

static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

// Starts a copy of this probe with the given mode arguments plus the shared ones.
System.Diagnostics.Process StartChild(IEnumerable<string> modeArguments)
{
	var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
	if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
	{
		start.ArgumentList.Add(typeof(ProbeArguments).Assembly.Location);
	}

	foreach (var argument in modeArguments.Concat(["--input", arguments.Input, "--type", arguments.DiscType, "--json", arguments.JsonPath]))
	{
		start.ArgumentList.Add(argument);
	}

	if (arguments.NativePath is { } nativePath)
	{
		start.ArgumentList.Add("--native-path");
		start.ArgumentList.Add(nativePath);
	}

	return System.Diagnostics.Process.Start(start)!;
}

// Crawls a Blu-ray in a child process. libbluray's BD-J (Java) menus can wedge the whole process — every thread,
// including any watchdog — so only a separate process can be stopped reliably. The child saves the map after every
// step; when it overruns it is killed and the last map saved is used. Null when nothing was mapped.
DiscMenuMap? CrawlInChild()
{
	var limit = TimeSpan.FromSeconds(Math.Min(arguments.TimeBudgetSeconds + 120, (deadline - DateTime.UtcNow).TotalSeconds - 30));
	List<string> crawlArguments =
	[
		"--crawl-only", "--time-budget", Number(arguments.TimeBudgetSeconds), "--max-menus", Number(arguments.MaxMenus),
		"--max-actions", Number(arguments.MaxActions), "--menu-language", arguments.MenuLanguage,
	];
	crawlArguments.AddRange(arguments.Screenshots ? ["--out", arguments.OutputFolder] : ["--no-screenshots"]);
	if (File.Exists(arguments.JsonPath))
	{
		File.Delete(arguments.JsonPath);
	}

	var stopped = false;
	using (var child = StartChild(crawlArguments))
	{
		if (!child.WaitForExit(limit))
		{
			Log($"crawl did not finish within {limit.TotalSeconds:0}s; stopping it and using the menus found so far");
			child.Kill(entireProcessTree: true);
			child.WaitForExit();
			stopped = true;
		}
		else if (child.ExitCode != 0)
		{
			Log($"crawl process exited with code {child.ExitCode}");
		}
	}

	if (!File.Exists(arguments.JsonPath) || DiscMenuJson.Deserialize(File.ReadAllText(arguments.JsonPath)) is not { } result)
	{
		Console.Error.WriteLine("Menu crawl failed: the crawl process produced no menu map.");
		return null;
	}

	if (stopped)
	{
		result.Complete = false;
		result.Warnings.Add("The menu crawl was stopped: the Blu-ray player stopped responding or the time limit was reached. Menus and buttons found before that are included.");
	}

	return result;
}

// Renders in a fresh copy of this process (its own Java VM for BD-J), so a VM wedged by the crawl cannot stall
// libvlc. Returns the map with screenshots, or the original map with a warning.
DiscMenuMap RenderInChild(DiscMenuMap target)
{
	var remaining = (int)(deadline - DateTime.UtcNow).TotalSeconds;
	if (remaining < 30)
	{
		target.Warnings.Add("Screenshots were skipped: the probe's time limit was reached during the crawl.");
		return target;
	}

	WriteMap(target);
	try
	{
		using var child = StartChild(
		[
			"--render-only", "--out", arguments.OutputFolder, "--screenshot-width", Number(arguments.ScreenshotWidth),
			"--settle-ms", Number(arguments.SettleMilliseconds), "--max-seconds", Number(remaining - 15),
		]);
		if (!child.WaitForExit(TimeSpan.FromSeconds(remaining - 5)))
		{
			child.Kill(entireProcessTree: true);
			target.Warnings.Add("Screenshots could not be finished within the probe's time limit.");
			return target;
		}

		if (child.ExitCode == 0 && DiscMenuJson.Deserialize(File.ReadAllText(arguments.JsonPath)) is { } rendered)
		{
			return rendered;
		}

		target.Warnings.Add($"Screenshots could not be rendered (renderer exit code {child.ExitCode}).");
	}
	catch (Exception ex)
	{
		target.Warnings.Add($"Screenshots could not be rendered: {ex.Message}");
		Log($"screenshot renderer process failed: {ex}");
	}

	return target;
}
