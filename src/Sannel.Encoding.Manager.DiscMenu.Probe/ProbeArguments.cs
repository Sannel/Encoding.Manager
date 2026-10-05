using System.Globalization;

namespace Sannel.Encoding.Manager.DiscMenu.Probe;

/// <summary>Command-line arguments of the probe.</summary>
internal sealed class ProbeArguments
{
	public const string Usage =
		"usage: probe --input <disc folder> --type dvd|bluray --json <map file> --out <screenshot folder> [--no-screenshots] " +
		"[--max-menus N] [--max-actions N] [--time-budget SECONDS] [--menu-language en] " +
		"[--screenshot-width PX] [--settle-ms MS] [--max-seconds N] [--native-path DIR]";

	public string Input { get; private set; } = string.Empty;

	/// <summary>"dvd" or "bluray".</summary>
	public string DiscType { get; private set; } = string.Empty;

	public string OutputFolder { get; private set; } = string.Empty;

	/// <summary>File the DiscMenuMap JSON is written to.</summary>
	public string JsonPath { get; private set; } = string.Empty;

	public bool Screenshots { get; private set; } = true;

	public int MaxMenus { get; private set; } = 64;

	public int MaxActions { get; private set; } = 500;

	public int TimeBudgetSeconds { get; private set; } = 240;

	public string MenuLanguage { get; private set; } = "en";

	public int ScreenshotWidth { get; private set; } = 960;

	public int SettleMilliseconds { get; private set; } = 1500;

	/// <summary>Total run time the probe allows itself; per-button screenshots stop before this.</summary>
	public int MaxSeconds { get; private set; } = 280;

	public string? NativePath { get; private set; }

	public static ProbeArguments Parse(string[] args)
	{
		var result = new ProbeArguments();
		for (var i = 0; i < args.Length; i++)
		{
			string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}.");
			int NextInt() => int.Parse(Next(), CultureInfo.InvariantCulture);

			switch (args[i])
			{
				case "--input": result.Input = Next(); break;
				case "--type": result.DiscType = Next().ToLowerInvariant(); break;
				case "--out": result.OutputFolder = Next(); break;
				case "--json": result.JsonPath = Next(); break;
				case "--no-screenshots": result.Screenshots = false; break;
				case "--max-menus": result.MaxMenus = NextInt(); break;
				case "--max-actions": result.MaxActions = NextInt(); break;
				case "--time-budget": result.TimeBudgetSeconds = NextInt(); break;
				case "--menu-language": result.MenuLanguage = Next(); break;
				case "--screenshot-width": result.ScreenshotWidth = NextInt(); break;
				case "--settle-ms": result.SettleMilliseconds = NextInt(); break;
				case "--max-seconds": result.MaxSeconds = NextInt(); break;
				case "--native-path": result.NativePath = Next(); break;
				default: throw new ArgumentException($"Unknown argument {args[i]}.");
			}
		}

		if (string.IsNullOrWhiteSpace(result.Input) || !Directory.Exists(result.Input))
		{
			throw new ArgumentException("--input must be an existing disc folder.");
		}

		if (result.DiscType is not ("dvd" or "bluray"))
		{
			throw new ArgumentException("--type must be dvd or bluray.");
		}

		if (result.Screenshots && string.IsNullOrWhiteSpace(result.OutputFolder))
		{
			throw new ArgumentException("--out is required unless --no-screenshots is given.");
		}

		if (string.IsNullOrWhiteSpace(result.JsonPath))
		{
			throw new ArgumentException("--json is required.");
		}

		return result;
	}
}
