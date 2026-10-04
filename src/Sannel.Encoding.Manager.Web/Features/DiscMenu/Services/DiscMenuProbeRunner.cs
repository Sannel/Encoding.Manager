using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Options;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

/// <summary>
/// Starts <c>Sannel.Encoding.Manager.DiscMenu.Probe</c> as a child process. The probe loads libdvdnav, libbluray,
/// libvlc and (for BD-J) a JVM, so a native crash or hang never takes the web app down, the JVM gets a fresh
/// process each time, and JAVA_HOME / LIBBLURAY_CP can be set per run.
/// </summary>
public class DiscMenuProbeRunner : IDiscMenuProbeRunner
{
	internal const string ProbeAssemblyName = "Sannel.Encoding.Manager.DiscMenu.Probe";
	internal const string ProbeFolderName = "disc-menu-probe";

	private readonly IProcessRunner _processRunner;
	private readonly DiscMenuOptions _options;
	private readonly ILogger<DiscMenuProbeRunner> _logger;

	public DiscMenuProbeRunner(IProcessRunner processRunner, IOptions<DiscMenuOptions> options, ILogger<DiscMenuProbeRunner> logger)
	{
		this._processRunner = processRunner;
		this._options = options.Value;
		this._logger = logger;
	}

	/// <inheritdoc />
	public async Task<DiscMenuProbeResult> RunAsync(string physicalPath, string discType, string screenshotFolder, CancellationToken ct)
	{
		var probe = this.LocateProbe();
		if (probe is null)
		{
			return new DiscMenuProbeResult { Unavailable = true, Error = $"The disc menu probe was not found (looked in {Path.Combine(AppContext.BaseDirectory, ProbeFolderName)}; set DiscMenu:ProbePath)." };
		}

		var jsonPath = Path.Combine(Path.GetTempPath(), $"disc-menu-{Guid.NewGuid():N}.json");
		var arguments = new List<string>(probe.Value.PrefixArguments)
		{
			"--input", physicalPath,
			"--type", discType,
			"--json", jsonPath,
			"--out", screenshotFolder,
			"--max-menus", this._options.MaxMenus.ToString(CultureInfo.InvariantCulture),
			"--max-actions", this._options.MaxActions.ToString(CultureInfo.InvariantCulture),
			"--time-budget", this._options.TimeBudgetSeconds.ToString(CultureInfo.InvariantCulture),
			"--menu-language", this._options.MenuLanguage,
			"--screenshot-width", this._options.ScreenshotWidth.ToString(CultureInfo.InvariantCulture),
			"--settle-ms", this._options.SettleMilliseconds.ToString(CultureInfo.InvariantCulture),
		};
		if (!string.IsNullOrWhiteSpace(this._options.NativeLibraryPath))
		{
			arguments.Add("--native-path");
			arguments.Add(this._options.NativeLibraryPath);
		}

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, this._options.ProbeTimeoutSeconds)));
		try
		{
			var result = await this._processRunner.RunAsync(probe.Value.FileName, arguments, this.BuildEnvironment(), timeout.Token);
			this._logger.LogDebug("Disc menu probe for {Path} exited with {ExitCode}: {Stderr}", physicalPath, result.ExitCode, result.StandardError);

			if (result.ExitCode == 2)
			{
				return new DiscMenuProbeResult { Unavailable = true, Error = LastLine(result.StandardError) ?? "A native library is missing." };
			}

			if (result.ExitCode != 0 || !File.Exists(jsonPath))
			{
				return new DiscMenuProbeResult { Error = $"The menu probe failed (exit code {result.ExitCode}): {LastLine(result.StandardError) ?? "no output"}" };
			}

			var map = DiscMenuJson.Deserialize(await File.ReadAllTextAsync(jsonPath, ct));
			return map is null
				? new DiscMenuProbeResult { Error = "The menu probe returned no data." }
				: new DiscMenuProbeResult { Map = map };
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			return new DiscMenuProbeResult { Error = $"The menu probe timed out after {this._options.ProbeTimeoutSeconds} seconds and was stopped." };
		}
		finally
		{
			TryDelete(jsonPath);
		}
	}

	private Dictionary<string, string?> BuildEnvironment()
	{
		var environment = new Dictionary<string, string?>();
		if (!string.IsNullOrWhiteSpace(this._options.JavaHome))
		{
			environment["JAVA_HOME"] = this._options.JavaHome;
		}

		var jar = this._options.LibBlurayJarPath ?? AutoDetectBdjJar();
		if (!string.IsNullOrWhiteSpace(jar))
		{
			environment["LIBBLURAY_CP"] = jar;
		}

		// An apphost-less probe started through the dotnet muxer needs to find the same runtime.
		var dotnetRoot = DotnetRoot();
		if (dotnetRoot is not null && Environment.GetEnvironmentVariable("DOTNET_ROOT") is null)
		{
			environment["DOTNET_ROOT"] = dotnetRoot;
		}

		return environment;
	}

	/// <summary>Finds the probe: an explicit path, or the copy published in disc-menu-probe/ next to the web app.</summary>
	private (string FileName, string[] PrefixArguments)? LocateProbe()
	{
		var candidate = this._options.ProbePath;
		if (string.IsNullOrWhiteSpace(candidate))
		{
			candidate = Path.Combine(AppContext.BaseDirectory, ProbeFolderName, ProbeAssemblyName + ".dll");
		}

		if (!File.Exists(candidate))
		{
			return null;
		}

		if (!candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
		{
			return (candidate, []);
		}

		var dotnet = DotnetHost();
		return (dotnet, [candidate]);
	}

	private static string DotnetHost()
	{
		var exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
		if (Environment.ProcessPath is { } processPath
			&& Path.GetFileName(processPath).Equals(exe, StringComparison.OrdinalIgnoreCase))
		{
			return processPath;
		}

		var root = DotnetRoot();
		return root is not null && File.Exists(Path.Combine(root, exe)) ? Path.Combine(root, exe) : exe;
	}

	/// <summary>The dotnet install root of the running runtime (…/shared/Microsoft.NETCore.App/x.y.z → …).</summary>
	private static string? DotnetRoot()
	{
		var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
		var root = Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", ".."));
		return Directory.Exists(Path.Combine(root, "shared")) ? root : null;
	}

	private static string? AutoDetectBdjJar()
	{
		if (!OperatingSystem.IsLinux() || !Directory.Exists("/usr/share/java"))
		{
			return null;
		}

		return Directory.EnumerateFiles("/usr/share/java", "libbluray-j2se*.jar").Order().LastOrDefault();
	}

	private static string? LastLine(string text) =>
		text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.LastOrDefault(l => !l.StartsWith("libdvd", StringComparison.Ordinal));

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{
		}
	}
}
