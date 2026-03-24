using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Scan.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>
/// Default implementation of <see cref="IHandBrakeService"/>.
/// Wraps HandBrakeCLI for scanning and encoding media files.
/// </summary>
public class HandBrakeService : IHandBrakeService
{
	private readonly IProcessRunner _processRunner;
	private readonly HandBrakeOptions _options;
	private readonly ILogger<HandBrakeService> _logger;
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly string _resolvedScanOutputPath;
	private readonly HandBrakeExecutable _executable;

	public string CliVersion { get; }

	public HandBrakeService(
		IProcessRunner processRunner,
		IOptions<HandBrakeOptions> options,
		ILogger<HandBrakeService> logger,
		IWebHostEnvironment env,
		IDbContextFactory<AppDbContext> dbFactory)
	{
		_processRunner = processRunner;
		_options = options.Value;
		_logger = logger;
		_dbFactory = dbFactory;

		// Resolve scan output path relative to content root
		_resolvedScanOutputPath = Path.IsPathRooted(_options.ScanOutputPath)
			? _options.ScanOutputPath
			: Path.Combine(env.ContentRootPath, _options.ScanOutputPath);

		// Locate executable (synchronous wait at startup — singleton, runs once)
		_executable = HandBrakeExecutableLocator
			.LocateAsync(_options.ExecutablePath, _processRunner)
			.GetAwaiter()
			.GetResult();

		// Verify version
		var versionResult = RunHandBrakeAsync(["--version"])
			.GetAwaiter()
			.GetResult();

		if (versionResult.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"HandBrakeCLI --version returned exit code {versionResult.ExitCode}. Stderr: {versionResult.StandardError}");
		}

		var versionOutput = !string.IsNullOrWhiteSpace(versionResult.StandardOutput)
			? versionResult.StandardOutput
			: versionResult.StandardError;

		if (!HandBrakeParser.TryParseVersion(versionOutput, out var detectedVersion) || detectedVersion is null)
		{
			throw new InvalidOperationException(
				$"Could not parse HandBrakeCLI version from output: {versionOutput}");
		}

		CliVersion = detectedVersion.ToString();

		if (!Version.TryParse(_options.MinimumVersion, out var minimumVersion))
		{
			throw new InvalidOperationException(
				$"Invalid HandBrake:MinimumVersion configuration value: {_options.MinimumVersion}");
		}

		if (detectedVersion < minimumVersion)
		{
			_logger.LogError(
				"HandBrakeCLI {DetectedVersion} is below the required minimum {MinimumVersion}",
				detectedVersion,
				minimumVersion);
			throw new InvalidOperationException(
				$"HandBrakeCLI {minimumVersion} or higher is required but {detectedVersion} was found.");
		}

		_logger.LogInformation(
			"HandBrakeCLI {Version} initialized successfully (minimum required: {MinimumVersion})",
			CliVersion,
			_options.MinimumVersion);
	}

	public async Task<HandBrakeScanResult> ScanAsync(string inputPath, bool forceRescan = false, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

		if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
		{
			throw new FileNotFoundException("Input file not found.", inputPath);
		}

		// Check DB cache unless a force rescan was requested (24-hour TTL)
		if (!forceRescan)
		{
			await using var cacheCtx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
			var cached = await cacheCtx.DiscScanCache
				.FirstOrDefaultAsync(c => c.InputPath == inputPath, ct)
				.ConfigureAwait(false);

			if (cached is not null && cached.CachedAt > DateTimeOffset.UtcNow.AddHours(-24))
			{
				_logger.LogInformation("Returning cached scan result for {InputPath} (cached at {CachedAt})", inputPath, cached.CachedAt);
				var cachedTitles = HandBrakeParser.ParseScan(cached.ScanJson);
				return new HandBrakeScanResult
				{
					IsSuccess = true,
					InputPath = inputPath,
					Titles = cachedTitles,
				};
			}
		}

		_logger.LogInformation("Starting scan of {InputPath}", inputPath);

		var result = await RunHandBrakeAsync(
			["--input", inputPath, "--title", "0", "--scan", "--json"],
			ct).ConfigureAwait(false);

		if (result.ExitCode != 0)
		{
			_logger.LogError(
				"HandBrakeCLI scan failed with exit code {ExitCode}. Stderr: {Stderr}",
				result.ExitCode,
				result.StandardError);

			return new HandBrakeScanResult
			{
				IsSuccess = false,
				InputPath = inputPath,
				Error = new HandBrakeError
				{
					ExitCode = result.ExitCode,
					Message = "HandBrakeCLI scan failed.",
					RawOutput = result.StandardError
				}
			};
		}

		var titles = HandBrakeParser.ParseScan(result.StandardOutput);

		// Persist scan output to disk
		string? outputFilePath = null;
		try
		{
			Directory.CreateDirectory(_resolvedScanOutputPath);
			var fileName = $"{Path.GetFileNameWithoutExtension(inputPath)}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
			outputFilePath = Path.Combine(_resolvedScanOutputPath, fileName);
			await File.WriteAllTextAsync(outputFilePath, result.StandardOutput, ct).ConfigureAwait(false);
			_logger.LogInformation("Scan result written to {OutputPath}", outputFilePath);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to write scan result to {ScanOutputPath}", _resolvedScanOutputPath);
		}

		// Persist to DB cache (replace any existing entry for this path)
		try
		{
			await using var cacheCtx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
			await cacheCtx.DiscScanCache
				.Where(c => c.InputPath == inputPath)
				.ExecuteDeleteAsync(ct)
				.ConfigureAwait(false);
			cacheCtx.DiscScanCache.Add(new DiscScanCache
			{
				InputPath = inputPath,
				ScanJson = result.StandardOutput,
				CachedAt = DateTimeOffset.UtcNow,
			});
			await cacheCtx.SaveChangesAsync(ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to cache disc scan result for {InputPath}", inputPath);
		}

		_logger.LogInformation("Scan of {InputPath} completed — {TitleCount} title(s) found", inputPath, titles.Count);

		return new HandBrakeScanResult
		{
			IsSuccess = true,
			InputPath = inputPath,
			ScanOutputFilePath = outputFilePath,
			Titles = titles
		};
	}

	public async Task<HandBrakeEncodeResult> EncodeAsync(
		HandBrakeJob job,
		IProgress<ProgressInfo>? progress = null,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		ArgumentException.ThrowIfNullOrWhiteSpace(job.InputPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(job.OutputPath);

		if (string.IsNullOrWhiteSpace(job.PresetName) && string.IsNullOrWhiteSpace(job.PresetFilePath))
		{
			throw new ArgumentException(
				"Either PresetName or PresetFilePath must be set on HandBrakeJob.");
		}

		if (!File.Exists(job.InputPath) && !Directory.Exists(job.InputPath))
		{
			throw new FileNotFoundException("Input file not found.", job.InputPath);
		}

		var outputDir = Path.GetDirectoryName(job.OutputPath);
		if (!string.IsNullOrEmpty(outputDir))
		{
			Directory.CreateDirectory(outputDir);
		}

		_logger.LogInformation("Starting encode: {InputPath} -> {OutputPath}", job.InputPath, job.OutputPath);

		var args = BuildEncodeArgs(job);
		var sw = Stopwatch.StartNew();
		var lastProgress = new ProgressInfo();

		var result = await RunHandBrakeWithCallbackAsync(
			args,
			line =>
			{
				var p = HandBrakeParser.ParseProgressLine(line);
				if (p is not null)
				{
					lastProgress = p;
					progress?.Report(p);
					_logger.LogDebug(
						"Encode progress: {Percent:F1}% ({Phase}, {Fps:F1} fps)",
						p.Percent,
						p.CurrentPhase,
						p.CurrentFps);
				}
			},
			ct).ConfigureAwait(false);

		sw.Stop();

		if (result.ExitCode != 0)
		{
			_logger.LogError(
				"HandBrakeCLI encode failed with exit code {ExitCode}. Stderr: {Stderr}",
				result.ExitCode,
				result.StandardError);

			return new HandBrakeEncodeResult
			{
				IsSuccess = false,
				OutputPath = job.OutputPath,
				Error = new HandBrakeError
				{
					ExitCode = result.ExitCode,
					Message = "HandBrakeCLI encode failed.",
					RawOutput = result.StandardError
				}
			};
		}

		_logger.LogInformation(
			"Encode completed: {OutputPath} in {Elapsed}",
			job.OutputPath,
			sw.Elapsed);

		return new HandBrakeEncodeResult
		{
			IsSuccess = true,
			OutputPath = job.OutputPath,
			ElapsedTime = sw.Elapsed,
			AverageFps = lastProgress.AverageFps
		};
	}

	private static List<string> BuildEncodeArgs(HandBrakeJob job)
	{
		var args = new List<string>
		{
			"--input", job.InputPath,
			"--output", job.OutputPath
		};

		if (!string.IsNullOrWhiteSpace(job.PresetFilePath))
		{
			args.Add("--preset-import-file");
			args.Add(job.PresetFilePath);
		}
		else if (!string.IsNullOrWhiteSpace(job.PresetName))
		{
			args.Add("--preset");
			args.Add(job.PresetName);
		}

		if (!string.IsNullOrWhiteSpace(job.AdditionalArgs))
		{
			// Split AdditionalArgs on whitespace boundaries.
			// This is intentionally simple; callers must sanitize inputs.
			foreach (var arg in job.AdditionalArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				args.Add(arg);
			}
		}

		return args;
	}

	private Task<ProcessResult> RunHandBrakeAsync(
		IEnumerable<string> handBrakeArgs,
		CancellationToken ct = default)
	{
		var allArgs = _executable.PrefixArgs.Concat(handBrakeArgs).ToList();
		return _processRunner.RunAsync(_executable.Binary, allArgs, ct);
	}

	private Task<ProcessResult> RunHandBrakeWithCallbackAsync(
		IEnumerable<string> handBrakeArgs,
		Action<string> onOutputLine,
		CancellationToken ct = default)
	{
		var allArgs = _executable.PrefixArgs.Concat(handBrakeArgs).ToList();
		return _processRunner.RunWithLineCallbackAsync(_executable.Binary, allArgs, onOutputLine, ct);
	}
}
